// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Coherence.Serializer;
    using Common;
    using Entities;
    using Log;
    using ProtocolDef;

    // TODO: Merge with RSL counterpart
    // Based on the pkg/entitymanager/InBuffer
    internal class ReceiveChangeBuffer
    {
        public static readonly TimeSpan MessageTTL = TimeSpan.FromSeconds(5f);

        private readonly Dictionary<Entity, IncomingEntityUpdate> entityDataByID = new(32);

        private readonly List<ExpirableMessage<CommandData>> commands = new(32);
        private readonly List<ExpirableMessage<InputData>> inputs = new(32);

        private readonly List<Entity> entitiesTaken = new(32);
        private readonly List<RefsInfo> refsToResolve = new(8);

        private readonly MessageResolver messageResolver;
        private readonly IEntityRegistry entityRegistry;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly Logger logger;

        public ReceiveChangeBuffer(IEntityRegistry entityRegistry, Logger logger, IDateTimeProvider dateTimeProvider = null)
        {
            this.messageResolver = new MessageResolver(entityRegistry);
            this.entityRegistry = entityRegistry;
            this.dateTimeProvider = dateTimeProvider ?? new SystemDateTimeProvider();
            this.logger = logger.With<ReceiveChangeBuffer>();
        }

        public void Clear()
        {
            logger.Trace("Clear");

            // Make sure to free all component data buffers
            foreach (var change in entityDataByID.Values)
            {
                change.Dispose();
            }
            entityDataByID.Clear();

            foreach (var cmd in commands)
            {
                cmd.Message.Dispose();
            }
            commands.Clear();

            foreach (var input in inputs)
            {
                input.Message.Dispose();
            }
            inputs.Clear();
        }

        public void AddChange(in IncomingEntityUpdate change)
        {
            change.TakeOwnershipOfComponents("ReceiveChangeBuffer.AddChange");

            Entity entity = change.Entity;

            bool changeExists = entityDataByID.TryGetValue(entity, out var existingChange);

            logger.Trace("Adding change",
                ("entity", entity),
                ("exists", changeExists),
                ("change", change));

            if (changeExists)
            {
                existingChange.Merge(change.ReleaseOwnershipOfComponents());
                entityDataByID[entity] = existingChange;
            }
            else
            {
                entityDataByID.Add(entity, change);
            }

            if (change.IsDestroy)
            {
                HandleMessagesReferencingDestroyedEntity(entity, commands);
                HandleMessagesReferencingDestroyedEntity(entity, inputs);
            }
        }

        public void AddCommand(CommandData command)
        {
            command.TakeOwnership("ReceiveChangeBuffer.AddCommand");

            logger.Trace("Adding command",
                ("entity", command.Entity),
                ("type", command.GetType()),
                ("sender", command.SenderParticipant),
                ("messageTarget", command.MessageTarget));

            commands.Add(new(command, dateTimeProvider.UtcNow + MessageTTL));
        }

        public void AddInput(InputData input)
        {
            input.TakeOwnership("ReceiveChangeBuffer.AddInput");

            logger.Trace("Adding input",
                ("entity", input.Entity),
                ("type", input.GetType()),
                ("sender", input.SenderParticipant),
                ("messageTarget", input.MessageTarget));

            inputs.Add(new(input, dateTimeProvider.UtcNow + MessageTTL));
        }

        public List<RefsInfo> GetRefsInfos()
        {
            refsToResolve.Clear();

            foreach (var (_, update) in entityDataByID)
            {
                // Ignore partial updates
                if (IsPartialUpdate(update))
                {
                    continue;
                }

                if (update.Meta.Operation == EntityOperation.Destroy)
                {
                    continue;
                }

                // Gather reference information
                var refsInfo = new RefsInfo(update);
                refsToResolve.Add(refsInfo);
            }

            return refsToResolve;
        }

        /// <summary>
        /// Moves resolvable IncomingEntityUpdates into the given buffer.
        /// Leftover updates stay in the ChangeBuffer.
        /// </summary>
        /// <param name="buffer">List of IncomingEntityUpdate to write valid updates into.</param>
        /// <param name="resolvableEntities">Set of Entities which are resolvable (not referencing an unknown entity).</param>
        public void TakeUpdates(List<IncomingEntityUpdate> buffer, IReadOnlyCollection<Entity> resolvableEntities)
        {
            refsToResolve.Clear();
            entitiesTaken.Clear();

            foreach (var (_, update) in entityDataByID)
            {
                // Ignore partial updates
                if (IsPartialUpdate(update))
                {
                    continue;
                }

                // Destroy can't come with any other change
                // and so can be handled immediately
                if (HandleDestroy(update, buffer))
                {
                    continue;
                }

                if (resolvableEntities.Contains(update.Entity))
                {
                    buffer.Add(update.ReleaseOwnershipOfComponents());
                    entitiesTaken.Add(update.Entity);
                }
            }

            foreach (var takenEntity in entitiesTaken)
            {
                entityDataByID.Remove(takenEntity);
            }

            // Sort the changes first by the operation type and then by the number of entity reference fields.
            // It is !!mandatory!! that creates are before updates in case the update references the created entity.
            // After that, same operation changes are sorted by the number of references so that the changes with
            // least references are first, so there is a smaller chance of pushing entities in the wrong order out of the core.
            // IMPORTANT: this doesn't fix the problem of SDK receiving create with
            // reference to non-existing entity because entities might have a cyclic
            // reference. The real fix for that must be on the SDK level.
            SortChanges(buffer);
        }

        private static void SortChanges(List<IncomingEntityUpdate> buff)
        {
            buff.Sort((lhs, rhs) =>
            {
                var operationDiff = GetOperationPriority(rhs.Meta.Operation) - GetOperationPriority(lhs.Meta.Operation);
                if (operationDiff != 0)
                {
                    return operationDiff;
                }

                return GetNumberOfRefs(lhs) - GetNumberOfRefs(rhs);
            });
        }

        private static int GetOperationPriority(EntityOperation operation)
        {
            return operation switch
            {
                EntityOperation.Create => 3,
                EntityOperation.Destroy => 2,
                EntityOperation.Update => 1,
                EntityOperation.Unknown => 0,
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };
        }

        private static int GetNumberOfRefs(IncomingEntityUpdate update)
        {
            var result = 0;

            var values = update.Components.Updates.Store.Sorted;
            for (var i = 0; i < values.Count; i++)
            {
                var (_, comp) = values[i];
                if (comp.HasRefFields)
                {
                    var entityRefsCount = comp.RefFieldsCount;
                    result += entityRefsCount;
                }
            }

            return result;
        }

        public void TakeCommands(Queue<CommandData> buffer)
        {
            TakeMessages(buffer, commands);
        }

        public void TakeInputs(Queue<InputData> buffer)
        {
            TakeMessages(buffer, inputs);
        }

        private void TakeMessages<T>(Queue<T> buffer, List<ExpirableMessage<T>> messages) where T : IMessageData<T>
        {
            if (messages.Count == 0)
            {
                return;
            }

            var now = dateTimeProvider.UtcNow;

            for (var i = messages.Count - 1; i >= 0; i--)
            {
                var message = messages[i].Message;

                if (messageResolver.IsResolvable(message))
                {
                    buffer.Enqueue(message.ReleaseOwnership());
                    messages.RemoveAt(i);
                    continue;
                }

                if (messages[i].HasExpired(now))
                {
                    messages[i].Message.Dispose();
                    messages.RemoveAt(i);

                    logger.Debug("Message expired",
                        ("entity", message.Entity),
                        ("typeID", message.TypeID),
                        ("sender", message.SenderParticipant),
                        ("messageTarget", message.MessageTarget));
                }
            }
        }

        private bool IsPartialUpdate(in IncomingEntityUpdate update)
        {
            if (update.Meta.Operation == EntityOperation.Create || update.Meta.Operation == EntityOperation.Destroy)
            {
                return false;
            }

            if (entityRegistry.EntityExists(update.Entity))
            {
                return false;
            }

            logger.Debug("Skipping partial update",
                ("entity", update.Entity),
                ("data", update.Components));

            return true;
        }

        private bool HandleDestroy(in IncomingEntityUpdate data, List<IncomingEntityUpdate> changes)
        {
            if (data.Meta.Operation != EntityOperation.Destroy)
            {
                return false;
            }

            entitiesTaken.Add(data.Entity);

            if (!entityRegistry.EntityExists(data.Entity))
            {
                data.Dispose();

                logger.Debug("Destroy for non-existing entity",
                    ("entity", data.Entity),
                    ("data", data.Components));

                return true;
            }

            changes.Add(data.ReleaseOwnershipOfComponents());

            return true;
        }

        private void HandleMessagesReferencingDestroyedEntity<T>(in Entity entity, List<ExpirableMessage<T>> messages) where T : IMessageData<T>
        {
            for (var i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i].Message.Entity == entity)
                {
                    logger.Debug("Message removed for destroyed entity",
                        ("entity", entity),
                        ("typeID", messages[i].Message.TypeID));

                    messages[i].Message.Dispose();
                    messages.RemoveAt(i);
                    continue;
                }

                messages[i].Message.NullEntityRefs(entity);
            }
        }
    }
}

#if UNITY_5_3_OR_NEWER

namespace Coherence.Cloud.Coroutines
{
    using Coherence.Cloud;
    using System;
    using System.Collections.Generic;

    public static class CloudRoomsServiceCoroutineExtensions
    {
        public static WaitForRequestResponse<IReadOnlyList<RoomData>> WaitForFetchRooms(this CloudRoomsService cloudRoomsService, string[] tags = null)
        {
            return new WaitForRequestResponse<IReadOnlyList<RoomData>>((fn) =>
            {
                cloudRoomsService.FetchRooms(fn, tags);
            });
        }

        public static WaitForRequestResponse<RoomData> WaitForCreateRoom(this CloudRoomsService cloudRoomsService, RoomCreationOptions roomCreationOptions)
        {
            return new WaitForRequestResponse<RoomData>((fn) =>
            {
                cloudRoomsService.CreateRoom(fn, roomCreationOptions ?? RoomCreationOptions.Default);
            });
        }

        public static WaitForRequestResponse<string> WaitForRemoveRoom(this CloudRoomsService cloudRoomsService, RoomData roomData)
        {
            return new WaitForRequestResponse<string>((fn) =>
            {
                cloudRoomsService.RemoveRoom(roomData, fn);
            });
        }

        [Obsolete("Use the " + nameof(RoomData) + " overload instead.")]
        public static WaitForRequestResponse<string> WaitForRemoveRoom(this CloudRoomsService cloudRoomsService, ulong uniqueID, string secret)
        {
            return new WaitForRequestResponse<string>((fn) =>
            {
#pragma warning disable CS0618
                cloudRoomsService.RemoveRoom(uniqueID, secret, fn);
#pragma warning restore CS0618
            });
        }
    }
}

#endif

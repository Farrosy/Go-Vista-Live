using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// Asset data Tur Kota: DialogueData per gedung dan QuestData "city-tour".
public static partial class CityTourBuilder {
    private const string DialogueFolder = "Assets/DialougeData/CityTour";
    private const string QuestPath = "Assets/Quests/CityTour.asset";
    // Bunyi ketik yang sama dengan dialog NPC lain.
    private const string VoiceClipGuid = "eb06d9483b9b50d4d9fdadc4a550d52e";

    private sealed class TourData {
        public DialogueData Intro, Reminder, AfterTour;
        public readonly Dictionary<string, DialogueData> StopDialogues = new Dictionary<string, DialogueData>();
        public QuestData Quest;
    }

    private static TourData BuildData(IReadOnlyList<CityTourContent.Stop> route) {
        Directory.CreateDirectory(DialogueFolder);
        var data = new TourData {
            Intro = BuildIntroDialogue(),
            Reminder = BuildDialogue(CityTourContent.ReminderDialogueId, CityTourContent.Reminder),
            AfterTour = BuildDialogue(CityTourContent.AfterTourDialogueId, CityTourContent.AfterTour),
        };
        foreach (CityTourContent.Stop stop in CityTourContent.Stops)
            data.StopDialogues[stop.ObjectiveId] = BuildDialogue(stop.DialogueId, stop.Lines);
        data.Quest = BuildQuest(route, data);
        AssetDatabase.SaveAssets();
        return data;
    }

    private static DialogueData BuildIntroDialogue() {
        DialogueData dialogue = LoadOrCreate<DialogueData>($"{DialogueFolder}/{CityTourContent.IntroDialogueId}.asset");
        var lines = new List<DialogueLine>();
        foreach (CityTourContent.Line line in CityTourContent.IntroBeforeQuestion) lines.Add(MainLine(line));

        DialogueLine question = MainLine(CityTourContent.IntroQuestion);
        question.isQuestion = true;
        question.question = new DialogueQuestion {
            questionId = "tour",
            options = new[] {
                new DialogueOption {
                    optionId = "yes", text = CityTourContent.IntroYes, isCorrect = true,
                    reply = new[] { ReplyLine(CityTourContent.IntroYesReply) },
                },
                new DialogueOption {
                    optionId = "later", text = CityTourContent.IntroLater, askAgainLater = true,
                    reply = new[] { ReplyLine(CityTourContent.IntroLaterReply) },
                },
            },
        };
        lines.Add(question);
        Fill(dialogue, CityTourContent.IntroDialogueId, lines);
        return dialogue;
    }

    private static DialogueData BuildDialogue(string id, IEnumerable<CityTourContent.Line> content) {
        DialogueData dialogue = LoadOrCreate<DialogueData>($"{DialogueFolder}/{id}.asset");
        var lines = new List<DialogueLine>();
        foreach (CityTourContent.Line line in content) lines.Add(MainLine(line));
        Fill(dialogue, id, lines);
        return dialogue;
    }

    private static void Fill(DialogueData dialogue, string id, List<DialogueLine> lines) {
        dialogue.dialogueId = id;
        dialogue.speakerName = CityTourContent.Vio;
        dialogue.lines = lines.ToArray();
        dialogue.charactersPerSecond = 38f;
        dialogue.punctuationPause = 0.12f;
        var voice = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(VoiceClipGuid));
        dialogue.voiceClips = voice != null ? new[] { voice } : new AudioClip[0];
        dialogue.pitchMin = 1.05f;
        dialogue.pitchMax = 1.3f;
        dialogue.voiceVolume = 0.8f;
        dialogue.focusCamera = true;
        dialogue.cameraDistance = 3.4f;
        dialogue.cameraHeight = 0.3f;
        dialogue.cameraSideOffset = 0.8f;
        EditorUtility.SetDirty(dialogue);
    }

    private static DialogueLine MainLine(CityTourContent.Line line) =>
        new DialogueLine { speaker = line.Speaker, text = line.Text, question = new DialogueQuestion { options = new DialogueOption[0] } };

    private static DialogueReplyLine ReplyLine(CityTourContent.Line line) =>
        new DialogueReplyLine { speaker = line.Speaker, text = line.Text };

    private static QuestData BuildQuest(IReadOnlyList<CityTourContent.Stop> route, TourData data) {
        QuestData quest = LoadOrCreate<QuestData>(QuestPath);
        quest.questId = CityTourContent.QuestId;
        quest.title = CityTourContent.QuestTitle;
        quest.description = CityTourContent.QuestDescription;
        quest.sequentialObjectives = true;
        quest.rewardCoins = CityTourContent.RewardCoins;

        var objectives = new List<QuestObjective> {
            new QuestObjective {
                objectiveId = CityTourContent.PlazaObjectiveId, description = CityTourContent.PlazaObjectiveText,
                type = QuestObjectiveType.GoToLocation, locationId = CityTourContent.PlazaLocationId,
            },
        };
        foreach (CityTourContent.Stop stop in route)
            objectives.Add(new QuestObjective {
                objectiveId = stop.ObjectiveId, description = stop.ObjectiveText,
                type = QuestObjectiveType.FinishDialogue, dialogue = data.StopDialogues[stop.ObjectiveId],
            });
        quest.objectives = objectives.ToArray();
        EditorUtility.SetDirty(quest);
        return quest;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}

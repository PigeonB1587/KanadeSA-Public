using Cysharp.Threading.Tasks;
using KanadeSA.Character;
using KanadeSA.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace KanadeSA.PreviewScene
{
    public class GlobalData
    {
        public static List<PetItem> petItems = new();
        public static List<WeaponItem> weaponItems = new();
        public static List<EmoteItem> emoteItems = new();

        public static List<ClothesItem> beardItems = new();
        public static List<ClothesItem> clotheItems = new();
        public static List<ClothesItem> glassesItems = new();
        public static List<ClothesItem> hatItems = new();
        public static List<ClothesItem> neckItems = new();
        public static List<ClothesItem> umbrellaItems = new();

        public static List<CharacterItem> characterItems = new();

        public static AnimationData animationActionItems = new();

        public static List<ClothesItem> otherItems = new();
        public static LocalizationRoot languageRoot = new();
        public static List<BoothBackgroundItem> boothBackgroundItems = new();

        public static List<(string, int[], string, Dictionary<string, int>)> styles = new();

        public static SceneSaveData _lastSceneSaveData = new();

        public static void LoadStylesFromFile() => styles = LoadJsonFromFile<List<(string, int[], string, Dictionary<string, int>)>>("styles.json");
        public static void LoadSceneSaveDataFromFile() => _lastSceneSaveData = LoadJsonFromFile<SceneSaveData>("last_scene.json", SceneSaveUtility.Settings);

        public static async UniTask SaveStylesAsync()
        {
            List<(string, int[], string, Dictionary<string, int>)> snapshot = new(styles);
            await SaveJsonAsync(JsonConvert.SerializeObject(snapshot), "styles.json");
        }
        public static async UniTask SaveSceneDataAsync(List<SARCharacterController> controllers, int index)
        {
            List<SARCharacterController> snapshot = new(controllers);
            string json = SceneSaveUtility.SerializeScene(snapshot, index);
            await SaveJsonAsync(json, "last_scene.json");
        }

        public static T LoadJsonFromFile<T>(string name, JsonSerializerSettings jsonSerializerSettings = null) where T : new()
        {
            string filePath = Path.Combine(Application.persistentDataPath, name);

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        T loaded = JsonConvert.DeserializeObject<T>(json, jsonSerializerSettings);
                        if (loaded != null)
                            return loaded;
                    }
                }
                catch (Exception)
                {

                }
            }

            return new T();
        }

        public static async UniTask SaveJsonAsync(string json, string name, CancellationToken ct = default)
        {
            string filePath = Path.Combine(Application.persistentDataPath, name);

            await UniTask.RunOnThreadPool(() =>
            {
                File.WriteAllText(filePath, json, Encoding.Unicode);
            }, cancellationToken: ct);

            await UniTask.SwitchToMainThread(ct);
        }
    }
}
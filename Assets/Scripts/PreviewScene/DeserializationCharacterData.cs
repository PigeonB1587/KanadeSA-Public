using Cysharp.Threading.Tasks;
using KanadeSA.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace KanadeSA.PreviewScene
{
    public class DeserializationCharacterData : MonoBehaviour
    {
        [SerializeField] private TextAsset petData;
        [SerializeField] private TextAsset weaponData;
        [SerializeField] private TextAsset emoteData;
        [SerializeField] private TextAsset beardData;
        [SerializeField] private TextAsset clotheData;
        [SerializeField] private TextAsset glassesData;
        [SerializeField] private TextAsset hatData;
        [SerializeField] private TextAsset neckData;
        [SerializeField] private TextAsset umbrellaData;
        [SerializeField] private TextAsset characterData;
        [SerializeField] private TextAsset animationActionData;
        [SerializeField] private TextAsset languageData;
        [SerializeField] private TextAsset itemData;
        [SerializeField] private TextAsset boothData;

        public async Task<UniTask> Deserialize()
        {
            var files = new List<(TextAsset asset, string fileName, Action<object> onLoaded)>
            {
                (petData, "_pet.json", v => GlobalData.petItems = v as List<PetItem> ?? new List<PetItem>()),
                (weaponData, "_weapon.json", v => GlobalData.weaponItems = v as List<WeaponItem> ?? new List<WeaponItem>()),
                (emoteData, "_emote.json", v => GlobalData.emoteItems = v as List<EmoteItem> ?? new List<EmoteItem>()),
                (beardData, "_beard.json", v => GlobalData.beardItems = v as List<ClothesItem> ?? new List<ClothesItem>()),
                (clotheData, "_clothe.json", v => GlobalData.clotheItems = v as List<ClothesItem> ?? new List<ClothesItem>()),
                (glassesData, "_glasses.json", v => GlobalData.glassesItems = v as List<ClothesItem> ?? new List<ClothesItem>()),
                (hatData, "_hat.json", v => GlobalData.hatItems = v as List<ClothesItem> ?? new List<ClothesItem>()),
                (neckData, "_neck.json", v => GlobalData.neckItems = v as List<ClothesItem> ?? new List<ClothesItem>()),
                (umbrellaData, "_umbrella.json", v => GlobalData.umbrellaItems = v as List<ClothesItem> ?? new List<ClothesItem>()),
                (characterData, "_character.json", v => GlobalData.characterItems = v as List<CharacterItem> ?? new List<CharacterItem>()),
                (animationActionData, "_action.json", v => GlobalData.animationActionItems = v as AnimationData ?? new AnimationData()),
                (languageData, "_language.json", v => GlobalData.languageRoot = v as LocalizationRoot ?? new LocalizationRoot()),
                (itemData, "_item.json", v => GlobalData.otherItems = v as List<ClothesItem> ?? new List<ClothesItem>()),
                (boothData, "_boothbg.json", v => GlobalData.boothBackgroundItems = v as List<BoothBackgroundItem> ?? new List<BoothBackgroundItem>())
            };

            foreach (var (asset, fileName, onLoaded) in files)
            {
                string json = asset != null ? asset.text : "";
                object result = null;

                if (!string.IsNullOrEmpty(json))
                {
                    switch (fileName)
                    {
                        case "_pet.json":
                            result = JsonConvert.DeserializeObject<List<PetItem>>(json);
                            break;
                        case "_weapon.json":
                            result = JsonConvert.DeserializeObject<List<WeaponItem>>(json);
                            break;
                        case "_emote.json":
                            result = JsonConvert.DeserializeObject<List<EmoteItem>>(json);
                            break;
                        case "_beard.json":
                        case "_clothe.json":
                        case "_glasses.json":
                        case "_hat.json":
                        case "_neck.json":
                        case "_umbrella.json":
                        case "_item.json":
                            result = JsonConvert.DeserializeObject<List<ClothesItem>>(json);
                            break;
                        case "_character.json":
                            result = JsonConvert.DeserializeObject<List<CharacterItem>>(json);
                            break;
                        case "_action.json":
                            result = JsonConvert.DeserializeObject<AnimationData>(json);
                            break;
                        case "_language.json":
                            result = JsonConvert.DeserializeObject<LocalizationRoot>(json);
                            break;
                        case "_boothbg.json":
                            result = JsonConvert.DeserializeObject<List<BoothBackgroundItem>>(json);
                            break;
                    }
                }

                onLoaded?.Invoke(result);
            }

            GlobalData.LoadStylesFromFile();
            GlobalData.LoadSceneSaveDataFromFile();

            await UniTask.Delay(750);
            return UniTask.CompletedTask;
        }
    }
}
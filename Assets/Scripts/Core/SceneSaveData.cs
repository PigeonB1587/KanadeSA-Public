using KanadeSA.Character;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KanadeSA.Core
{
    [Serializable]
    public class SceneSaveData
    {
        public List<CharacterSaveData> characters { get; set; } = new List<CharacterSaveData>();

        public int boothIndex { get; set; } = 0;
        public int formatVersion { get; set; } = 0;
        public string saveTime { get; set; }
    }

    [Serializable]
    public class CharacterSaveData
    {
        public Vector3 position { get; set; }

        public int[] itemIndex { get; set; }
        public List<int> itemequipmentIndex { get; set; }
        public string actionKey { get; set; }
        public Dictionary<string, int> gunSkinIndex { get; set; }

        public bool faceRight { get; set; }
        public bool petFaceRight { get; set; }

        public float shadowlightIntensity { get; set; }
        public Color playerColor { get; set; }

        public float localPlayerScale { get; set; }
        public float playerYOffset { get; set; }

        public float targetAngle { get; set; }
        public float playerAngle { get; set; }

        public bool isPaused { get; set; }
        public float[] animationProgress { get; set; }
        public int controlAnimationTrackIndex { get; set; }
        public SpineCharacterAnimationMode animationMode { get; set; }

        public Vector3 petLocalPosition { get; set; }
    }

    public static class SceneSaveUtility
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter>
            {
                new Vector3Converter(),
                new ColorConverter(),
            }
        };

        public static string SerializeScene(List<SARCharacterController> characterControllerList, int boothIndex)
        {
            List<CharacterSaveData> characters = new();

            foreach (var item in characterControllerList)
            {
                characters.Add(new CharacterSaveData()
                {
                    position = item.transform.parent.position,
                    itemIndex = item.itemIndex,
                    itemequipmentIndex = item.itemequipmentIndex,
                    actionKey = item.actionKey,
                    gunSkinIndex = item.gunSkinIndex,
                    faceRight = item.faceRight,
                    petFaceRight = item.petFaceRight,
                    shadowlightIntensity = item.shadowlightIntensity,
                    playerColor = item.playerColor,
                    localPlayerScale = item.localPlayerScale,
                    playerYOffset = item.playerYOffset,
                    targetAngle = item.targetAngle,
                    playerAngle = item.playerAngle,
                    isPaused = item.isPaused,
                    animationProgress = item.animationProgress,
                    controlAnimationTrackIndex = item.controlAnimationTrackIndex,
                    animationMode = item.spineCharacterAnimation,
                    petLocalPosition = item.petController.transform.localPosition
                });
            }

            SceneSaveData sceneSaveData = new()
            {
                characters = characters,
                boothIndex = boothIndex,
                formatVersion = 0,
                saveTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            return JsonConvert.SerializeObject(sceneSaveData, Settings);
        }

        public static SceneSaveData DeserializeScene(string json) => JsonConvert.DeserializeObject<SceneSaveData>(json, Settings);
    }

    public class Vector3Converter : JsonConverter<Vector3>
    {
        public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("x");
            writer.WriteValue(value.x);
            writer.WritePropertyName("y");
            writer.WriteValue(value.y);
            writer.WritePropertyName("z");
            writer.WriteValue(value.z);
            writer.WriteEndObject();
        }

        public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Vector3.zero;

            var obj = serializer.Deserialize<Dictionary<string, float>>(reader);
            if (obj != null && obj.TryGetValue("x", out float x) && obj.TryGetValue("y", out float y) && obj.TryGetValue("z", out float z))
                return new Vector3(x, y, z);

            return Vector3.zero;
        }
    }

    public class ColorConverter : JsonConverter<Color>
    {
        public override void WriteJson(JsonWriter writer, Color value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("r");
            writer.WriteValue(value.r);
            writer.WritePropertyName("g");
            writer.WriteValue(value.g);
            writer.WritePropertyName("b");
            writer.WriteValue(value.b);
            writer.WritePropertyName("a");
            writer.WriteValue(value.a);
            writer.WriteEndObject();
        }

        public override Color ReadJson(JsonReader reader, Type objectType, Color existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Color.white;

            var obj = serializer.Deserialize<Dictionary<string, float>>(reader);
            if (obj != null && obj.TryGetValue("r", out float r) && obj.TryGetValue("g", out float g)
                && obj.TryGetValue("b", out float b) && obj.TryGetValue("a", out float a))
                return new Color(r, g, b, a);

            return Color.white;
        }
    }
}
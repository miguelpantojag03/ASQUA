using System.Text.Json;

namespace UnityEngine
{
    /// <summary>
    /// Sustituto de UnityEngine.JsonUtility para correr las pruebas EditMode con `dotnet test`.
    /// Imita lo que usan las pruebas: solo campos públicos, enums como número y los campos que
    /// faltan en el JSON conservan el valor con que se inicializan. Dentro de Unity se usa el real.
    /// </summary>
    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            IgnoreReadOnlyProperties = true,
        };

        public static string ToJson(object obj)
        {
            return obj == null ? "" : JsonSerializer.Serialize(obj, obj.GetType(), Options);
        }

        public static T FromJson<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
    }
}

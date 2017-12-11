using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using LitJson;
using ScriptingMod.Extensions;
using ScriptingMod.Tools;
using UnityEngine;

namespace ScriptingMod
{
    // based on: https://github.com/Mervill/UnityLitJson/blob/master/Source/Unity/UnityTypeBindings.cs
    internal static class LitJsonTypeBindings
    {
        // ReSharper disable once AssignNullToNotNullAttribute
        private static readonly Type _exporterFuncType = typeof(JsonMapper).Assembly.GetType(typeof(ExporterFunc<>).FullName, true);
        private static readonly MethodInfo _miRegisterExporter = ReflectionTools.GetMethod(typeof(JsonMapper), nameof(JsonMapper.RegisterExporter));
        private static readonly MethodInfo _miIgnore = ReflectionTools.GetMethod(typeof(LitJsonTypeBindings), nameof(Ignore));

        private static bool _registerd;

        public static void Register()
        {

            if (_registerd) return;
            _registerd = true;

            // If you seralize using WriteProperty()
            // LitJson will attempt to bind property
            // names to class members instead of using
            // an importer.

            // Value types that are currently unsupported
            JsonMapper.RegisterExporter<float>((o, w) => w.Write(Convert.ToDouble(o)));
            JsonMapper.RegisterImporter<double, float>(input => Convert.ToSingle(input));
            JsonMapper.RegisterExporter<decimal>((obj, writer) => writer.Write(Convert.ToString(obj)));
            JsonMapper.RegisterImporter<string, decimal>(input => Convert.ToDecimal(input));
            JsonMapper.RegisterImporter<int, long>(input => Convert.ToInt64(input));

            // Conversions for complex types
            JsonMapper.RegisterExporter<Type>(WriteType);
            JsonMapper.RegisterImporter<string, Type>(ReadType);
            JsonMapper.RegisterExporter<Vector2>(WriteVector2);
            JsonMapper.RegisterExporter<Vector2i>(WriteVector2i);
            JsonMapper.RegisterExporter<Vector2xz>(WriteVector2xz);
            JsonMapper.RegisterExporter<Vector3>(WriteVector3);
            JsonMapper.RegisterExporter<Vector3i>(WriteVector3i);
            JsonMapper.RegisterExporter<Vector4>(WriteVector4);
            JsonMapper.RegisterExporter<Quaternion>(WriteQuaternion);
            JsonMapper.RegisterExporter<Color>(WriteColor);
            JsonMapper.RegisterExporter<Color32>(WriteColor32);
            JsonMapper.RegisterExporter<Bounds>(WriteBounds);
            JsonMapper.RegisterExporter<Rect>(WriteRect);
            JsonMapper.RegisterExporter<RectOffset>(WriteRectOffset);
            JsonMapper.RegisterExporter<ClientInfo>(WriteClientInfo);
            JsonMapper.RegisterExporter<PlayerProfile>(WritePlayerProfile);

            // Register all Entity and their derived types as ignored
            var entityTypes = typeof(Entity).Assembly.GetExportedTypes().Where(t => typeof(Entity).IsAssignableFrom(t));
            RegisterIgnoredTypes(entityTypes);

            Log.Out("Registered all custom JSON type bindings.");
        }

        /// <summary>
        /// Creates for each given type generics for JsonMapper.RegisterExporter, LitJson.ExporterFunc, and Ignore,
        /// and registers the empty method as exporter for this type
        /// </summary>
        /// <param name="types"></param>
        private static void RegisterIgnoredTypes(IEnumerable<Type> types)
        {
            foreach (var type in types)
            {
                var exporter = Delegate.CreateDelegate(_exporterFuncType.MakeGenericType(type), _miIgnore.MakeGenericMethod(type));
                _miRegisterExporter.MakeGenericMethod(type).Invoke(null, new object[] { exporter });
                Log.Debug($"Registered ignored JSON type {type}.");
            }
        }

        private static void RegisterExporter(Type type, Action<object, JsonWriter> exporter)
        {
            // TODO: Try if this works better...
        }

        private static Type ReadType(string s)
        {
            return Type.GetType(s);
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private static void Ignore<T>(T obj, JsonWriter w)
        {
            // intentionally left blank
        }

        private static void WriteType(Type v, JsonWriter w)
        {
            w.Write(v.FullName);
        }

        private static void WriteColor(Color v, JsonWriter w)
        {
            // Color can be implicitly converted to Color32, see https://docs.unity3d.com/ScriptReference/Color32.html
            WriteColor32(v, w);
        }

        private static void WriteColor32(Color32 v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("r", v.r);
            w.WriteProperty("g", v.g);
            w.WriteProperty("b", v.b);
            w.WriteProperty("a", v.a);
            w.WriteObjectEnd();
        }

        private static void WriteRectOffset(RectOffset v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("top", v.top);
            w.WriteProperty("left", v.left);
            w.WriteProperty("bottom", v.bottom);
            w.WriteProperty("right", v.right);
            w.WriteObjectEnd();
        }

        private static void WriteRect(Rect v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("x", v.x);
            w.WriteProperty("y", v.y);
            w.WriteProperty("width", v.width);
            w.WriteProperty("height", v.height);
            w.WriteObjectEnd();
        }

        private static void WriteBounds(Bounds v, JsonWriter w)
        {
            w.WriteObjectStart();

            w.WritePropertyName("center");
            WriteVector3(v.center, w);

            w.WritePropertyName("size");
            WriteVector3(v.size, w);

            w.WriteObjectEnd();
        }

        private static void WriteQuaternion(Quaternion v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("x", v.x);
            w.WriteProperty("y", v.y);
            w.WriteProperty("z", v.z);
            w.WriteProperty("w", v.w);
            w.WriteObjectEnd();
        }

        private static void WriteVector4(Vector4 v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("x", v.x);
            w.WriteProperty("y", v.y);
            w.WriteProperty("z", v.z);
            w.WriteProperty("w", v.w);
            w.WriteObjectEnd();
        }

        private static void WriteVector3i(Vector3i v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("x", v.x);
            w.WriteProperty("y", v.y);
            w.WriteProperty("z", v.z);
            w.WriteObjectEnd();
        }

        private static void WriteVector3(Vector3 v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("x", v.x);
            w.WriteProperty("y", v.y);
            w.WriteProperty("z", v.z);
            w.WriteObjectEnd();
        }

        private static void WriteVector2(Vector2 v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("x", v.x);
            w.WriteProperty("y", v.y);
            w.WriteObjectEnd();
        }

        private static void WriteVector2i(Vector2i v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("x", v.x);
            w.WriteProperty("y", v.y);
            w.WriteObjectEnd();
        }

        private static void WriteVector2xz(Vector2xz v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("x", v.x);
            w.WriteProperty("z", v.z);
            w.WriteObjectEnd();
        }

        private static void WriteClientInfo(ClientInfo v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("playerName", v.playerName);
            w.WriteProperty("steamId", v.playerId);
            w.WriteProperty("ownerSteamId", v.ownerId);
            w.WriteProperty("entityId", v.entityId);
            w.WriteProperty("ip", v.ip);
            w.WriteProperty("ping", v.ping);
            w.WriteObjectEnd();
        }

        private static void WritePlayerProfile(PlayerProfile v, JsonWriter w)
        {
            w.WriteObjectStart();
            w.WriteProperty("archetype", v.Archetype);
            w.WriteProperty("gender", v.IsMale ? "male" : "female");
            w.WriteProperty("hairName", v.HairName);

            w.WritePropertyName("hairColor");
            WriteColor(v.HairColor, w);

            w.WritePropertyName("skinColor");
            WriteColor(v.SkinColor, w);

            w.WritePropertyName("eyeColor");
            WriteColor(v.EyeColor, w);

            // Let's omit v.Dna for now...

            w.WriteProperty("beardName", v.BeardName);
            w.WriteObjectEnd();
        }

    }
}

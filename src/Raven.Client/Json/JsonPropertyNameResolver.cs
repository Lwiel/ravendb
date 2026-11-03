using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Raven.Client.Documents.Conventions;
using Raven.Client.Json.Serialization;

namespace Raven.Client.Json
{
    internal static class JsonPropertyNameResolver
    {
        private const string NewtonsoftJsonPropertyAttributeName = "Newtonsoft.Json.JsonPropertyAttribute";

        private static readonly ConcurrentDictionary<MemberInfo, string> _systemTextJsonCache = new();

        public static string GetJsonPropertyName(MemberInfo member, DocumentConventions conventions)
        {
            if ((conventions ?? DocumentConventions.Default).Serialization is not IJsonPropertyNameResolver resolver)
                return null;

            string name = resolver.GetJsonPropertyName(member);
            return string.IsNullOrEmpty(name) ? null : name;
        }

        internal static string GetSystemTextJsonPropertyName(MemberInfo member)
        {
            string result = _systemTextJsonCache.GetOrAdd(member, ResolveSystemTextJsonName);
            return result.Length == 0 ? null : result;
        }

        private static string ResolveSystemTextJsonName(MemberInfo member)
        {
            return member.GetCustomAttribute<JsonPropertyNameAttribute>(inherit: true)?.Name
                   ?? GetNewtonsoftJsonPropertyName(member)
                   ?? GetDataContractMemberName(member)
                   ?? string.Empty;
        }

        private static string GetNewtonsoftJsonPropertyName(MemberInfo member)
        {
            foreach (object attribute in member.GetCustomAttributes(inherit: true))
            {
                Type attributeType = attribute.GetType();
                if (attributeType.FullName == NewtonsoftJsonPropertyAttributeName)
                    return (string)attributeType.GetProperty("PropertyName")?.GetValue(attribute);
            }

            return null;
        }

        private static string GetDataContractMemberName(MemberInfo member)
        {
            for (Type type = member.DeclaringType; type != null; type = type.BaseType)
            {
                if (type.IsDefined(typeof(DataContractAttribute), inherit: false))
                    return member.GetCustomAttribute<DataMemberAttribute>(inherit: true)?.Name;
            }

            return null;
        }
    }
}

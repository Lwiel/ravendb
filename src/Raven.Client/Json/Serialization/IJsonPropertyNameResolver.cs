using System.Reflection;

namespace Raven.Client.Json.Serialization
{
    public interface IJsonPropertyNameResolver
    {
        string GetJsonPropertyName(MemberInfo member);
    }
}

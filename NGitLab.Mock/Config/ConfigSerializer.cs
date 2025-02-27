using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

#pragma warning disable MA0009 // Add regex evaluation timeout

namespace NGitLab.Mock.Config;

internal sealed class ConfigSerializer
{
    private static readonly Regex s_variableTemplateRegex = new(@"{{|}}|{[a-zA-Z_][\w\.-]+}", RegexOptions.Compiled);

    private readonly Dictionary<string, object> _configs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _resolvedDictionary = new(StringComparer.Ordinal);

    public void DefineVariable(string name, object value)
    {
        _variables[name] = value;
    }

    public static string Serialize(GitLabConfig config)
    {
        var serializer = new SerializerBuilder().DisableAliases().Build();
        return serializer.Serialize(new Dictionary<string, GitLabConfig>(StringComparer.OrdinalIgnoreCase)
        {
            { "gitlab", config },
        });
    }

    public void Deserialize(string content)
    {
        var deserializer = new DeserializerBuilder().Build();
        var data = deserializer.Deserialize<Dictionary<string, object>>(content);
        foreach (var config in data)
        {
            if (string.Equals(config.Key, "variables", StringComparison.OrdinalIgnoreCase))
            {
                if (config.Value is not Dictionary<object, object> dict)
                    throw new InvalidOperationException("Variables config is invalid, expected dictionary");

                foreach (var pair in dict)
                {
                    if (pair.Key is not string key)
                        throw new InvalidOperationException("Variables config is invalid, expected keys as string");

                    DefineVariable(key, pair.Value);
                }
            }
            else
            {
                _configs[config.Key] = config.Value;
            }
        }
    }

    public bool TryGet<T>(string name, ref T value)
        where T : class
    {
        if (!_configs.TryGetValue(name, out var config))
            return true;

        object v = value;
        config = ExpandVariables(config);
        if (!TryConvert(typeof(T), config, ref v))
            return false;

        value = (T)v;
        return true;
    }

    private object ExpandVariables(object value)
    {
        return value == null ? null : ExpandVariables(value, new Stack<string>());
    }

    private object ExpandVariables(object value, Stack<string> stack)
    {
        if (value is string str)
        {
            var matches = s_variableTemplateRegex.Matches(str).Cast<Match>().ToArray();
            if (matches.Length == 0)
                return Environment.ExpandEnvironmentVariables(str);

            if (matches.Length == 1 && matches[0].Length == str.Length)
                return ResolveVariable(str.Trim('{', '}'), stack);

            var bld = new StringBuilder();
            var index = 0;
            foreach (var match in matches)
            {
                if (index < match.Index)
                    bld.Append(Environment.ExpandEnvironmentVariables(str[index..match.Index]));

                if (string.Equals(match.Value, "{{", StringComparison.Ordinal))
                    bld.Append('{');
                else if (string.Equals(match.Value, "}}", StringComparison.Ordinal))
                    bld.Append('}');
                else
                    bld.Append(ResolveVariable(match.Value.Trim('{', '}'), stack));

                index = match.Index + match.Length;
            }

            if (index < str.Length)
                bld.Append(Environment.ExpandEnvironmentVariables(str[index..]));

            return bld.ToString();
        }

        if (value is List<object> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                list[i] = ExpandVariables(list[i], stack);
            }

            return list;
        }

        if (value is Dictionary<object, object> dictionary)
        {
            foreach (var key in dictionary.Keys.ToArray())
            {
                dictionary[key] = ExpandVariables(dictionary[key], stack);
            }

            return dictionary;
        }

        return value;
    }

    private object ResolveVariable(string name, Stack<string> stack)
    {
        if (stack.Contains(name, StringComparer.Ordinal))
            throw new InvalidOperationException($"Cyclic variable resolution of '{name}'");

        switch (name.ToLowerInvariant())
        {
            case "new_guid":
                return Guid.NewGuid().ToString("D");
        }

        if (_resolvedDictionary.TryGetValue(name, out var value))
            return value;

        if (!_variables.TryGetValue(name, out value))
            throw new InvalidOperationException($"Variable '{name}' not found");

        stack.Push(name);
        value = ExpandVariables(value, stack);
        stack.Pop();
        _resolvedDictionary[name] = value;

        return value;
    }

    public static bool TryConvert<T>(object valueObj, out T value)
    {
        object v = null;
        if (TryConvert(typeof(T), valueObj, ref v))
        {
            value = (T)v;
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryConvert(Type type, object valueObj, ref object value)
    {
        if (type == typeof(object) || type.IsInstanceOfType(valueObj))
        {
            value = valueObj;
            return true;
        }

        if (type == typeof(bool))
        {
            if (valueObj is string valueString && bool.TryParse(string.IsNullOrEmpty(valueString) ? "false" : valueString, out var valueBool))
            {
                value = valueBool;
                return true;
            }

            return false;
        }

        if (type == typeof(long))
        {
            if (valueObj is string valueString && long.TryParse(valueString, out var valueLong))
            {
                value = valueLong;
                return true;
            }

            return false;
        }

        if (type == typeof(string))
        {
            if (valueObj == null)
            {
                value = null;
                return true;
            }

            if (valueObj is string valueString)
            {
                value = valueString;
                return true;
            }

            return false;
        }

        if (type.IsEnum)
        {
            if (valueObj is string valueString && TryParseEnum(type, valueString, out var valueEnum))
            {
                value = valueEnum;
                return true;
            }

            return false;
        }

        if (type == typeof(DateTime))
        {
            if (valueObj is string valueString && (DateTime.TryParseExact(valueString, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valueDate) || DateTime.TryParse(valueString, CultureInfo.InvariantCulture, DateTimeStyles.None, out valueDate)))
            {
                value = valueDate;
                return true;
            }

            return false;
        }

        if (type == typeof(DateTimeOffset))
        {
            if (valueObj is string valueString && (DateTimeOffset.TryParseExact(valueString, "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valueDate) || DateTimeOffset.TryParse(valueString, CultureInfo.InvariantCulture, DateTimeStyles.None, out valueDate)))
            {
                value = valueDate;
                return true;
            }

            return false;
        }

        if (valueObj == null)
        {
            value = null;
            return true;
        }

        // Nullable
        if (type.IsGenericType)
        {
            var genericArgs = type.GetGenericArguments();
            if (genericArgs.Length == 1 && genericArgs[0].IsValueType)
            {
                var nullableType = typeof(Nullable<>).MakeGenericType(genericArgs);
                if (!type.IsAssignableFrom(nullableType))
                    return false;

                object v = null;
                if (!TryConvert(genericArgs[0], valueObj, ref v))
                    return false;

                value = Activator.CreateInstance(nullableType, v);
                return true;
            }
        }

        if (value == null && type.IsClass && !type.IsAbstract && !type.IsArray)
        {
            value = Activator.CreateInstance(type);
        }

        var success = false;
        if (typeof(IDictionary).IsAssignableFrom(type))
        {
            var itf = type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IDictionary<,>)
                ? type
                : type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IDictionary<,>));

            if (itf != null)
            {
                var args = itf.GetGenericArguments();
                if (args.Length != 2 || args[0] != typeof(string) || valueObj is not Dictionary<object, object> valueDict)
                    return false;

                var itemType = args[1];
                var dict = value as IDictionary;
                if (dict == null)
                {
                    var dictType = typeof(Dictionary<,>).MakeGenericType(args);
                    if (!type.IsAssignableFrom(dictType))
                        return false;

                    dict = (IDictionary)Activator.CreateInstance(dictType);
                }

                foreach (var key in valueDict.Keys.Select(x => x as string ?? throw new InvalidOperationException($"Not supported key: {x}")).ToArray())
                {
                    object v = null;
                    if (!TryConvert(itemType, valueDict[key], ref v))
                        return false;

                    dict[key] = v;
                }

                value = dict;
                success = true;
            }
        }
        else if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                    return false;

                var itemType = type.GetElementType();
                if (itemType == null || valueObj is not List<object> valueArr)
                    return false;

                var arr = Array.CreateInstance(itemType, valueArr.Count);
                for (var i = 0; i < valueArr.Count; i++)
                {
                    object v = null;
                    if (!TryConvert(itemType, valueArr[i], ref v))
                        return false;

                    arr.SetValue(v, i);
                }

                value = arr;
                return true;
            }

            var itf = type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>)
                ? type
                : type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IList<>));

            if (itf != null)
            {
                var args = itf.GetGenericArguments();
                if (args.Length != 1 || valueObj is not List<object> valueArr)
                    return false;

                var itemType = args[0];
                var list = value as IList;
                if (list == null)
                {
                    var listType = typeof(List<>).MakeGenericType(itemType);
                    if (!type.IsAssignableFrom(listType))
                        return false;

                    list = (IList)Activator.CreateInstance(listType);
                }

                foreach (var item in valueArr)
                {
                    object v = null;
                    if (!TryConvert(itemType, item, ref v))
                        return false;

                    list.Add(v);
                }

                value = list;
                success = true;
            }
        }

        if (type.IsClass)
        {
            if (valueObj is not Dictionary<object, object> tempDict)
                return success;

            var valueDict = tempDict.ToDictionary(x => (string)x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var property in type.GetProperties())
            {
                if (!valueDict.TryGetValue(property.Name, out var vObj))
                    continue;

                var v = property.GetValue(value);
                if (v == null && !property.CanWrite)
                    return false;

                if (!TryConvert(property.PropertyType, vObj, ref v))
                    return false;

                if (property.CanWrite)
                    property.SetValue(value, v);
            }

            return true;
        }

        return success;
    }

    private static bool TryParseEnum(Type type, string value, out object result)
    {
        try
        {
            result = Enum.Parse(type, value, ignoreCase: true);
            return true;
        }
        catch
        {
            result = default;
            return false;
        }
    }
}

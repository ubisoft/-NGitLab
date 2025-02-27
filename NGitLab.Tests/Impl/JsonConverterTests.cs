using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NGitLab.Impl.Json;
using NUnit.Framework;

namespace NGitLab.Tests.Impl;

public class JsonConverterTests
{
    [Test]
    public void Test_DeserializeNullToSupportedValueType_Succeeds()
    {
        var json = """
            {
              "a_boolean": null,
              "a_date_time": null,
              "a_double": null,
              "an_int32": null,
              "an_int64": null
            }
            """;
        var obj = Serializer.Deserialize<MyDataContract>(json);

        Assert.That(obj, Is.Not.Null);
        Assert.That(obj.SomeBool, Is.EqualTo(false));
        Assert.That(obj.SomeDateTime, Is.EqualTo(DateTime.MinValue));
        Assert.That(obj.SomeDouble, Is.EqualTo(0.0));
        Assert.That(obj.SomeInt32, Is.EqualTo(0));
        Assert.That(obj.SomeInt64, Is.EqualTo(0L));
    }

    [Test]
    public void Test_DeserializeNullToUnsupportedValueType_Throws()
    {
        var json = @"{ ""a_uint32"": null }";
        var ex = Assert.Throws<JsonException>(() => Serializer.Deserialize<MyDataContract>(json));
        Assert.That(ex.Message, Does.StartWith("The JSON value could not be converted to System.UInt32."));
    }

    [Test]
    public void Test_DeserializeStringToInt32()
    {
        var json = @"{ ""an_int32"": ""1234"" }";
        var obj = Serializer.Deserialize<MyDataContract>(json);
        Assert.That(obj, Is.Not.Null);
        Assert.That(obj.SomeInt32, Is.EqualTo(1234));
    }

    [Test]
    public void Test_DeserializeStringToInt64_Succeeds()
    {
        var json = @"{ ""an_int64"": ""-1234"" }";
        var obj = Serializer.Deserialize<MyDataContract>(json);
        Assert.That(obj, Is.Not.Null);
        Assert.That(obj.SomeInt64, Is.EqualTo(-1234L));
    }

    [Test]
    public void Test_DeserializeStringToDouble_Succeeds()
    {
        var json = @"{ ""a_double"": ""-1234.5"" }";
        var obj = Serializer.Deserialize<MyDataContract>(json);
        Assert.That(obj, Is.Not.Null);
        Assert.That(obj.SomeDouble, Is.EqualTo(-1234.5d));
    }

    [TestCase("2022-01-12", DateTimeKind.Unspecified)]
    [TestCase("2022-01-12T22:49:21.552Z", DateTimeKind.Utc)]
    [TestCase("2022-01-12T22:49:21.552+00:00", DateTimeKind.Utc)]
    public void Test_DeserializeStringToDateTime_SupportsMultipleFormats(string input, DateTimeKind kind)
    {
        var json = $@"{{ ""a_date_time"": ""{input}"" }}";
        var obj = Serializer.Deserialize<MyDataContract>(json);
        Assert.That(obj, Is.Not.Null);
        Assert.That(obj.SomeDateTime.Kind, Is.EqualTo(kind));
        Assert.That(obj.SomeDateTime.Date, Is.EqualTo(new DateTime(2022, 1, 12).Date));
    }

    public class MyDataContract
    {
        [JsonPropertyName("a_boolean")]
        public bool SomeBool { get; set; }

        [JsonPropertyName("a_date_time")]
        public DateTime SomeDateTime { get; set; }

        [JsonPropertyName("a_double")]
        public double SomeDouble { get; set; }

        [JsonPropertyName("an_int32")]
        public int SomeInt32 { get; set; }

        [JsonPropertyName("an_int64")]
        public long SomeInt64 { get; set; }

        [JsonPropertyName("a_uint32")]
        public uint SomeUInt32 { get; set; }
    }
}

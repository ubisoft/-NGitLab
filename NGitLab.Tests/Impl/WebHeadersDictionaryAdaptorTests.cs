using System.Linq;
using System.Net;
using NGitLab.Impl;
using NUnit.Framework;

namespace NGitLab.Tests.Impl;

public class WebHeadersDictionaryAdaptorTests
{
    private static void VerifyAdaptor(WebHeaderCollection headers)
    {
        var sut = new WebHeadersDictionaryAdaptor(headers);

        Assert.Multiple(() =>
        {
            Assert.That(sut, Has.Count.EqualTo(headers.Count));
            Assert.That(sut.Keys.Count(), Is.EqualTo(headers.Count));
            Assert.That(sut.Values.Count(), Is.EqualTo(headers.Count));
        });

        foreach ((var k, var v) in sut)
        {
            Assert.That(sut.TryGetValue(k, out var actual), Is.True);
            Assert.That(v, Is.EquivalentTo(actual));
        }
    }

    [Test]
    public void Test_empty_header_collection_works_correctly()
    {
        VerifyAdaptor([]);
    }

    [Test]
    public void Test_single_header_collection_works_correctly()
    {
        VerifyAdaptor(new()
        {
            { HttpRequestHeader.Authorization, "Bearer 12345" },
        });
    }

    [Test]
    public void Test_multiple_header_collection_works_correctly()
    {
        VerifyAdaptor(new()
        {
            { "Accept-Charset: utf-8" },
            { "Accept-Language: de; q=1.0, en; q=0.5" },
            { "Cookie: $Version=1; Skin=new;" },
            { "X-Forwarded-For: client1, proxy1, proxy2" },
        });
    }

    [Test]
    public void Test_empty_and_null_header_values_works_correctly()
    {
        VerifyAdaptor(new()
        {
            { "foo", "" },
            { "bar", null },
        });
    }
}

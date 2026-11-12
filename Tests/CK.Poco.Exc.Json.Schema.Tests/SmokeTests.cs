using CK.Core;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Schema.Tests;

[TestFixture]
public class SmokeTests
{
    public interface ISample : IPoco
    {
        string Name { get; set; }
    }

    [Test]
    public async Task the_engine_runs_with_the_Poco_JSON_exchange_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSchemaSupport ), typeof( ISample ) );
        await using var auto = (await configuration.RunSuccessfullyAsync()).CreateAutomaticServices();
        // CommonPocoJsonSupport is registered by CommonPocoJsonSchemaSupport: the JSON exchange works.
        var o = auto.Services.GetRequiredService<IPocoFactory<ISample>>().Create( s => s.Name = "x" );
        o.ToString().ShouldBe( """{"Name":"x"}""" );
    }
}

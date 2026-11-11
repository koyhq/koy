using System.Reflection;
using System.Text.Json.Nodes;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

/// <summary>
/// v3 → v4 anahtar tablosunun tutarlılığı. Altın dosyadaki her düz anahtar ya tabloda ya da bilerek atılanlar listesinde
/// olmalı; tablodaki her hedef gerçekten var olan bir bölüm ve alan olmalı. Yazım hatası ya da unutulmuş alan CI'da kırılır.
/// </summary>
public class SettingsKeyCoverageTests
{
    private static readonly JsonObject Fixture = (JsonObject)JsonNode.Parse(SettingsFile.FixtureV3)!;

    [Fact]
    public void Every_key_in_the_v3_golden_file_is_mapped_or_explicitly_dropped()
    {
        var unknown = Fixture.Select(p => p.Key)
            .Where(k => !SettingsKeyMap.TopLevel.Contains(k)
                        && !SettingsKeyMap.V3ToV4.ContainsKey(k)
                        && !SettingsKeyMap.Dropped.Contains(k))
            .ToList();

        Assert.Empty(unknown);
    }


    [Fact]
    public void Every_mapping_target_is_a_real_section_and_field()
    {
        var problems = new List<string>();
        foreach (var (v3Key, (section, name)) in SettingsKeyMap.V3ToV4)
        {
            var sectionProperty = typeof(AppSettings).GetProperty(section);
            if (sectionProperty is null)
            {
                problems.Add($"{v3Key} -> section '{section}' yok");
                continue;
            }

            if (sectionProperty.PropertyType.GetProperty(name) is null)
                problems.Add($"{v3Key} -> {section}.{name} yok");
        }

        Assert.Empty(problems);
    }

    [Fact]
    public void No_two_v3_keys_map_to_the_same_v4_field()
    {
        var targets = SettingsKeyMap.V3ToV4.Values.GroupBy(t => $"{t.Section}.{t.Name}").Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.Empty(targets);
    }
}

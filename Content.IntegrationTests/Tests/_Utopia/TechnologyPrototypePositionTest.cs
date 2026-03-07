using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Utopia;

[TestFixture]
public sealed class TechnologyPrototypePositionTests
{
    [Test]
    public async Task TechnologyPrototypePositionsAreUniqueTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();

        var fails = new List<string>();
        var positions = new Dictionary<(ProtoId<TechDisciplinePrototype> Discipline, Vector2 Position), string>();

        await server.WaitAssertion(() =>
        {
            foreach (var techProto in protoManager.EnumeratePrototypes<TechnologyPrototype>())
            {
                Vector2 position = techProto.Position;
                var discipline = techProto.Discipline;
                var key = (discipline, position);

                if (!positions.TryAdd(key, techProto.Name))
                {
                    fails.Add($"ID: {techProto.ID} Position - {position}, Discipline: {discipline}. Conflicts with ID: {positions[key]}");
                }
            }
        });

        if (fails.Count > 0)
        {
            var msg = string.Join("\n", fails) + "\n" + "Technology position is already taken within the same discipline!";
            Assert.Fail(msg);
        }

        await pair.CleanReturnAsync();
    }
}

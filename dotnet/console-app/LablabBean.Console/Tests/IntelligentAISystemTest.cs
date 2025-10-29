using Akka.Actor;
using Arch.Core;
using LablabBean.AI.Actors.Systems;
using LablabBean.AI.Core.Components;
using LablabBean.AI.Core.Models;
using LablabBean.Game.Core.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SadRogue.Primitives;

namespace LablabBean.Console.Tests;

/// <summary>
/// Simple test harness for IntelligentAISystem without TUI
/// </summary>
public class IntelligentAISystemTest
{
    public static async Task RunTest(IServiceProvider serviceProvider)
    {
        try
        {
            var logger = serviceProvider.GetRequiredService<ILogger<IntelligentAISystemTest>>();
            var actorSystem = serviceProvider.GetRequiredService<ActorSystem>();
            var intelligentAISystem = serviceProvider.GetRequiredService<IntelligentAISystem>();

            logger.LogInformation("=== IntelligentAISystem Test Starting ===");

            // Create test world
            var world = World.Create();
            logger.LogInformation("Created ECS world");

            // Create test entities
            var boss1 = world.Create(
                new IntelligentAI(AICapability.TacticalAdaptation | AICapability.QuestGeneration, 2.0f),
                new Position(10, 10),
                new Health(150, 150),
                new Name("Test Boss"),
                new Renderable('@', Color.Red)
            );

            var employee1 = world.Create(
                new IntelligentAI(AICapability.Dialogue | AICapability.Memory, 1.5f),
                new Position(15, 15),
                new Health(50, 50),
                new Name("Test Employee"),
                new Renderable('e', Color.Blue)
            );

            logger.LogInformation("Created 2 test entities: Boss (ID={BossId}) and Employee (ID={EmployeeId})",
                boss1.Id, employee1.Id);

            // Test 1: Actor Spawning
            logger.LogInformation("=== Test 1: Actor Spawning ===");
            intelligentAISystem.Update(world, 0.016f);
            await Task.Delay(1000); // Wait for actors to spawn

            // Test 2: Multiple Updates
            logger.LogInformation("=== Test 2: Multiple Updates ===");
            for (int i = 0; i < 5; i++)
            {
                intelligentAISystem.Update(world, 0.016f);
                await Task.Delay(200);
                logger.LogInformation("Update {Count} complete", i + 1);
            }

            // Test 3: Player Proximity
            logger.LogInformation("=== Test 3: Player Proximity ===");
            var player = world.Create(
                new Player("Test Player"),
                new Position(12, 12), // Near boss at (10,10)
                new Health(100, 100)
            );
            logger.LogInformation("Created player at (12, 12) - near boss at (10, 10)");

            for (int i = 0; i < 3; i++)
            {
                intelligentAISystem.Update(world, 0.016f);
                await Task.Delay(500);
                logger.LogInformation("Player proximity update {Count}", i + 1);
            }

            // Test 4: Graceful Shutdown
            logger.LogInformation("=== Test 4: Graceful Shutdown ===");
            intelligentAISystem.Shutdown();
            await Task.Delay(1000);

            logger.LogInformation("=== IntelligentAISystem Test Complete ===");

            world.Dispose();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"TEST FAILED: {ex.Message}");
            System.Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}

using Arch.Core;
using LablabBean.AI.Core.Components;
using LablabBean.AI.Core.Models;
using LablabBean.Game.Core.Components;
using Microsoft.Extensions.Logging;
using SadRogue.Primitives;

namespace LablabBean.Console.Services;

/// <summary>
/// Factory for creating test entities with IntelligentAI components
/// </summary>
public class IntelligentEntityFactory
{
    private readonly ILogger<IntelligentEntityFactory> _logger;

    public IntelligentEntityFactory(ILogger<IntelligentEntityFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a boss entity with intelligent AI capabilities
    /// </summary>
    public Entity CreateBoss(World world, Point position, string name = "Angry Boss")
    {
        var entity = world.Create(
            new IntelligentAI(
                AICapability.TacticalAdaptation | AICapability.QuestGeneration,
                decisionCooldown: 2.0f
            ),
            new Position(position.X, position.Y),
            new Health(150, 150),
            new Name(name),
            new Renderable('@', new Color(255, 0, 0)) // Red @ symbol
        );

        _logger.LogInformation("Created boss entity '{Name}' at ({X}, {Y})", name, position.X, position.Y);
        return entity;
    }

    /// <summary>
    /// Creates an employee entity with intelligent AI capabilities
    /// </summary>
    public Entity CreateEmployee(World world, Point position, string name = "Helpful Employee")
    {
        var entity = world.Create(
            new IntelligentAI(
                AICapability.Dialogue | AICapability.Memory,
                decisionCooldown: 1.5f
            ),
            new Position(position.X, position.Y),
            new Health(50, 50),
            new Name(name),
            new Renderable('e', new Color(100, 150, 255)) // Blue 'e' symbol
        );

        _logger.LogInformation("Created employee entity '{Name}' at ({X}, {Y})", name, position.X, position.Y);
        return entity;
    }

    /// <summary>
    /// Creates multiple boss entities in a level
    /// </summary>
    public List<Entity> CreateBossesInLevel(World world, int count = 3)
    {
        var bosses = new List<Entity>();
        var random = new Random();

        for (int i = 0; i < count; i++)
        {
            var x = random.Next(5, 75);
            var y = random.Next(5, 35);
            var bossName = $"Boss #{i + 1}";

            var boss = CreateBoss(world, new Point(x, y), bossName);
            bosses.Add(boss);
        }

        _logger.LogInformation("Created {Count} boss entities in level", count);
        return bosses;
    }

    /// <summary>
    /// Creates multiple employee entities in a level
    /// </summary>
    public List<Entity> CreateEmployeesInLevel(World world, int count = 5)
    {
        var employees = new List<Entity>();
        var random = new Random();

        for (int i = 0; i < count; i++)
        {
            var x = random.Next(5, 75);
            var y = random.Next(5, 35);
            var employeeName = $"Employee #{i + 1}";

            var employee = CreateEmployee(world, new Point(x, y), employeeName);
            employees.Add(employee);
        }

        _logger.LogInformation("Created {Count} employee entities in level", count);
        return employees;
    }

    /// <summary>
    /// Creates a mixed test scenario with both bosses and employees
    /// </summary>
    public (List<Entity> bosses, List<Entity> employees) CreateTestScenario(World world)
    {
        var bosses = new List<Entity>
        {
            CreateBoss(world, new Point(10, 10), "The Micromanager"),
            CreateBoss(world, new Point(60, 20), "VP of Deadlines")
        };

        var employees = new List<Entity>
        {
            CreateEmployee(world, new Point(15, 15), "Chatty Colleague"),
            CreateEmployee(world, new Point(25, 12), "Coffee Expert"),
            CreateEmployee(world, new Point(55, 22), "Bug Hunter")
        };

        _logger.LogInformation("Created test scenario: {BossCount} bosses, {EmployeeCount} employees",
            bosses.Count, employees.Count);

        return (bosses, employees);
    }
}

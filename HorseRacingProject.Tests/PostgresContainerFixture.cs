using HorseRacingAPI.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace HorseRacingProject.Tests;

public class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("horseracing_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private Respawner _respawner = null!;

    public string ConnectionString => _container.GetConnectionString();

    private const string MaxParticipantsTriggerSql = """
        CREATE OR REPLACE FUNCTION public.fn_check_max_participants()
            RETURNS trigger
            LANGUAGE 'plpgsql'
        AS $BODY$
          DECLARE
              v_max   INT;
              v_count INT;
          BEGIN
              IF NEW."Status" = 'Confirmed' AND (OLD."Status" IS DISTINCT FROM 'Confirmed') THEN
                  PERFORM pg_advisory_xact_lock(hashtext(NEW."RaceID"::text));

                  SELECT "MaxParticipants" INTO v_max
                  FROM "Races"
                  WHERE "RaceID" = NEW."RaceID";

                  IF v_max IS NOT NULL THEN
                      SELECT COUNT(*) INTO v_count
                      FROM "Registrations"
                      WHERE "RaceID" = NEW."RaceID" AND "Status" = 'Confirmed';

                      IF v_count >= v_max THEN
                          RAISE EXCEPTION 'Race has reached the maximum number of participants (%)', v_max
                              USING ERRCODE = 'P0001';
                      END IF;
                  END IF;
              END IF;
              RETURN NEW;
          END;
        $BODY$;

        DROP TRIGGER IF EXISTS trg_check_max_participants ON public."Registrations";

        CREATE TRIGGER trg_check_max_participants
            BEFORE UPDATE
            ON public."Registrations"
            FOR EACH ROW
            EXECUTE FUNCTION public.fn_check_max_participants();
        """;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        DbContextOptions<HorseRacingDataContext> options = new DbContextOptionsBuilder<HorseRacingDataContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var context = new HorseRacingDataContext(options);
        await context.Database.EnsureCreatedAsync();
        await context.Database.ExecuteSqlRawAsync(MaxParticipantsTriggerSql);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"]
        });
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
}

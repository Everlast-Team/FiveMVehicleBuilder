using Spectre.Console.Cli;
using FiveMVehicleBuilder.Cli.Commands;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("fvb");
    config.SetApplicationVersion("0.1.0");

    config.AddCommand<InitCommand>("init")
        .WithDescription("Bibliothek initialisieren: Pfad festlegen, Git-Repo anlegen");

    config.AddCommand<ImportCommand>("import")
        .WithDescription("FiveM-Resource importieren, ggf. in mehrere Records zerlegen");

    config.AddCommand<ListCommand>("list")
        .WithDescription("Alle Records auflisten");

    config.AddCommand<CheckCommand>("check")
        .WithDescription("Prüfungen ausführen, Bericht ausgeben");

    config.AddBranch("record", record =>
    {
        record.SetDescription("Records verwalten");
        record.AddCommand<RecordInfoCommand>("info")
            .WithDescription("Details zu einem Record anzeigen");
        record.AddCommand<RecordRemoveCommand>("remove")
            .WithDescription("Record löschen");
        record.AddCommand<RecordMoveCommand>("move")
            .WithDescription("Record in anderes Pack verschieben");
    });

    config.AddBranch("variant", variant =>
    {
        variant.SetDescription("Varianten verwalten");
        variant.AddCommand<VariantAddCommand>("add")
            .WithDescription("Neue Variante zu einem Record hinzufügen");
        variant.AddCommand<VariantRemoveCommand>("remove")
            .WithDescription("Variante entfernen");
        variant.AddCommand<VariantListCommand>("list")
            .WithDescription("Varianten eines Records auflisten");
    });

    config.AddBranch("livery", livery =>
    {
        livery.SetDescription("Livery-Zuordnungen verwalten");
        livery.AddCommand<LiveryAssignCommand>("assign")
            .WithDescription("Livery einem Department zuordnen");
        livery.AddCommand<LiveryRemoveCommand>("remove")
            .WithDescription("Livery-Zuordnung entfernen");
        livery.AddCommand<LiveryListCommand>("list")
            .WithDescription("Livery-Zuordnungen auflisten");
    });

    config.AddBranch("pack", pack =>
    {
        pack.SetDescription("Packs verwalten");
        pack.AddCommand<PackCreateCommand>("create")
            .WithDescription("Neues Pack erstellen");
        pack.AddCommand<PackAddCommand>("add")
            .WithDescription("Record zu einem Pack hinzufügen");
        pack.AddCommand<PackRemoveCommand>("remove")
            .WithDescription("Record aus einem Pack entfernen");
        pack.AddCommand<PackListCommand>("list")
            .WithDescription("Alle Packs auflisten");
    });

    config.AddCommand<BuildCommand>("build")
        .WithDescription("Pack als FiveM-Resource bauen");

    config.AddCommand<TestCommand>("test")
        .WithDescription("Einzel-Resource zum Testen bauen");

    config.AddCommand<ExportCommand>("export")
        .WithDescription("Framework-Export erstellen (json, lua, sql)");
});

return app.Run(args);

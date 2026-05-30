namespace Andy.Engine.SweBench.Grading.Constants;

/// <summary>
/// Static table of repo+version test specs, ported from swebench
/// constants/python.py. Currently populated for django/django (M1 subset);
/// add repos here as the subset grows (each addition must be re-validated via gold mode).
/// </summary>
public static class RepoTestSpecs
{
    private const string TestDjango = "./tests/runtests.py --verbosity 2 --settings=test_sqlite --parallel 1";
    private const string TestDjangoNoParallel = "./tests/runtests.py --verbosity 2";

    // django 3.0-5.2 locale eval_commands (sed locale-gen variant used for 3.x).
    private static readonly string[] DjangoLocale3X =
    {
        "sed -i '/en_US.UTF-8/s/^# //g' /etc/locale.gen && locale-gen",
        "export LANG=en_US.UTF-8",
        "export LANGUAGE=en_US:en",
        "export LC_ALL=en_US.UTF-8",
    };

    // astropy uses pytest. NOTE: swebench's constants/python.py defines TEST_PYTEST twice — first
    // as "pytest --no-header -rA --tb=no -p no:cacheprovider", then redefines it to a bare
    // "pytest -rA". The prebuilt eval images (which we reuse) were validated against the FULLER
    // command: "--no-header" is required, because astropy's pytest-header plugin otherwise
    // escalates the nose-style `setup(self)` deprecation to an error, failing whole test classes
    // (e.g. astropy 3.1 test_header.py). So we use the fuller TEST_PYTEST that matches the images.
    private const string TestPytest = "pytest --no-header -rA --tb=no -p no:cacheprovider";
    private const string TestAstropyPytest = "pytest -rA -vv -o console_output_style=classic --tb=no";
    private const string AstropyInstall = "python -m pip install -e .[test] --verbose";

    private static readonly Dictionary<string, Dictionary<string, RepoTestSpec>> Map =
        new(StringComparer.Ordinal)
        {
            ["django/django"] = BuildDjango(),
            ["astropy/astropy"] = BuildAstropy(),
        };

    /// <summary>Look up the spec for an instance's repo+version, or null if unknown.</summary>
    public static RepoTestSpec? TryGet(string repo, string version)
    {
        if (Map.TryGetValue(repo, out var byVersion) && byVersion.TryGetValue(version, out var spec))
            return spec;
        return null;
    }

    public static bool IsRepoSupported(string repo) => Map.ContainsKey(repo);

    private static Dictionary<string, RepoTestSpec> BuildDjango()
    {
        var d = new Dictionary<string, RepoTestSpec>(StringComparer.Ordinal);

        // 1.4-1.6: setup.py install, no locale.
        foreach (var v in new[] { "1.4", "1.5", "1.6" })
            d[v] = new RepoTestSpec { TestCmd = TestDjango, Install = "python setup.py install" };

        // 1.7-2.2: setup.py install + UTF-8 locale exports.
        foreach (var v in new[] { "1.7", "1.8", "1.9", "1.10", "1.11", "2.0", "2.1", "2.2" })
            d[v] = new RepoTestSpec
            {
                TestCmd = TestDjango,
                Install = "python setup.py install",
                EvalCommands = new[]
                {
                    "export LANG=en_US.UTF-8",
                    "export LC_ALL=en_US.UTF-8",
                    "export PYTHONIOENCODING=utf8",
                    "export LANGUAGE=en_US:en",
                },
            };

        // 3.0-3.2: editable install + locale-gen.
        foreach (var v in new[] { "3.0", "3.1", "3.2" })
            d[v] = new RepoTestSpec
            {
                TestCmd = TestDjango,
                Install = "python -m pip install -e .",
                EvalCommands = DjangoLocale3X,
            };

        // 4.0/4.1/4.2/5.0-5.2: editable install, no eval_commands.
        foreach (var v in new[] { "4.0", "4.1", "4.2", "5.0", "5.1", "5.2" })
            d[v] = new RepoTestSpec { TestCmd = TestDjango, Install = "python -m pip install -e ." };

        // Per-version override: 1.9 runs without --settings/--parallel.
        d["1.9"] = d["1.9"] with { TestCmd = TestDjangoNoParallel };

        return d;
    }

    private static Dictionary<string, RepoTestSpec> BuildAstropy()
    {
        var d = new Dictionary<string, RepoTestSpec>(StringComparer.Ordinal);

        // 3.0-5.2 (and v5.3): test_cmd = TEST_PYTEST, editable test install. No eval_commands.
        foreach (var v in new[] { "3.0", "3.1", "3.2", "4.1", "4.2", "4.3", "5.0", "5.1", "5.2" })
            d[v] = new RepoTestSpec { TestCmd = TestPytest, Install = AstropyInstall };

        // 0.1-1.3: test_cmd = TEST_ASTROPY_PYTEST (verbose/classic output).
        foreach (var v in new[] { "1.1", "1.2", "1.3" })
            d[v] = new RepoTestSpec { TestCmd = TestAstropyPytest, Install = AstropyInstall };

        return d;
    }
}

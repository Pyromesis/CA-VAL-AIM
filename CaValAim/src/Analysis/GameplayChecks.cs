using Godot;
using CaValAim.Core;
using CaValAim.Game;
using CaValAim.Game.Weapon;
using CaValAim.Audio;

namespace CaValAim.Analysis;

/// <summary>Regression cases for real movement paths, timing and spatial audio.</summary>
static class GameplayChecks
{
    public static void Run(Action<string> report)
    {
        void Check(bool ok, string label) => report($"  {(ok ? "PASS" : "FAIL")} gameplay: {label}");
        Check(UI.TaskSearch.Matches("REACCION periferica", "Reacción periférica") &&
            UI.TaskSearch.Matches("micro", "Microcorrecciones") &&
            !UI.TaskSearch.Matches("tracking invisible", "Tracking continuo") && UI.TaskSearch.Matches("  ", "Cualquiera"),
            "library search ignores accents/case, matches partial words and requires all terms");
        foreach (string section in Modes.TrainingCatalog.Sections)
        {
            var tasks = Modes.ModeRegistry.All.Select(make => make())
                .Where(m => m.Category != "Agents" && Modes.TrainingCatalog.SectionOf(m) == section).ToArray();
            Check(tasks.Length >= 20, $"training section {section}: {tasks.Length} exercises");
        }
        Check(Modes.TrainingCatalog.All.Select(v => v.Key).Distinct().Count() == 180,
            "180 unique progression keys");
        foreach (var variant in Modes.TrainingCatalog.All)
            Check(Modes.ModeRegistry.Find(variant.Key)?.Invoke().Key == variant.Key,
                $"catalog resolves {variant.Key}");
        var prescription = PersonalDrills.For(new Diagnosis("FLICK_SMALL", Skill.Flicking, Severity.Major,
            "", "", "", Array.Empty<string>(), Array.Empty<string>(), 12));
        Check(prescription != null && Modes.ModeRegistry.Find(prescription)?.Invoke().Key == prescription,
            "missing authored drill gets a reproducible personal task");
        Check(!RunAnalysis.StaticTargetMode("flickx:course-dynamic-0-0") &&
            RunAnalysis.StaticTargetMode("flickx:course-precision-0-0"),
            "moving click targets cannot masquerade as sensitivity braking errors");

        // Recommended routines: a new player gets the complete baseline, partial profiles gather evidence before
        // acting on Tips, and a well-rated profile keeps its personalised diagnosis-first plan.
        var emptyPlan = CoachPlan.Build(new SkillProfile());
        Check(emptyPlan.IsOnboarding && !emptyPlan.IsProvisional && emptyPlan.Steps.Count == CoachPlan.Starter.Length &&
            emptyPlan.TotalRuns == CoachPlan.Starter.Sum(s => s.Target) && emptyPlan.Steps.Select(s => s.Drill).Distinct().Count() == CoachPlan.Starter.Length &&
            emptyPlan.Steps.All(s => Modes.ModeRegistry.Find(s.Drill)?.Invoke().Key == s.Drill),
            $"recommended empty profile: all {CoachPlan.Starter.Length} baseline drills and {emptyPlan.TotalRuns} runs resolve");
        var emptyAgain = CoachPlan.Build(new SkillProfile());
        Check(string.Join("|", emptyPlan.Queue()) == string.Join("|", emptyAgain.Queue()),
            "recommended baseline is deterministic, including repetitions and recoil");
        var partialProfile = new SkillProfile { RunsAnalyzed = 4 };
        partialProfile.Skills.Add(new SkillRating(Skill.Flicking, 1.5f, 0.8f, 20, ""));
        partialProfile.Problems.Add(new Diagnosis("POSSIBLE_ONLY", Skill.Flicking, Severity.Tip, "Possible", "One clue", "",
            Array.Empty<string>(), new[] { "spider" }, 2));
        var partialPlan = CoachPlan.Build(partialProfile);
        Check(partialPlan.IsProvisional && !partialPlan.IsOnboarding && partialPlan.Steps.Count > 0 &&
            partialPlan.Steps.All(s => s.Source == null || s.Source.Severity != Severity.Tip) && partialPlan.Steps.Any(s => s.Source == null),
            "recommended partial profile prioritises evidence over unconfirmed Tips");
        var fullProfile = new SkillProfile { RunsAnalyzed = 40, OverallTier = 2 };
        foreach (var skill in Enum.GetValues<Skill>())
            fullProfile.Skills.Add(new SkillRating(skill, skill == Skill.Movement ? 0.5f : 2, 0.8f, 30, ""));
        var major = new Diagnosis("RUN_GUN", Skill.Movement, Severity.Major, "Confirmed movement issue", "Measured misses", "",
            Array.Empty<string>(), new[] { "counterstrafe" }, 30);
        fullProfile.Problems.Add(major);
        var fullPlan = CoachPlan.Build(fullProfile);
        Check(!fullPlan.IsProvisional && !fullPlan.IsOnboarding && fullPlan.Steps.Count <= 4 && fullPlan.Steps[0].Source == major,
            "recommended complete profile keeps its confirmed diagnosis first");
        var balancedProfile = new SkillProfile { RunsAnalyzed = 40, OverallTier = 2 };
        foreach (var skill in Enum.GetValues<Skill>())
            balancedProfile.Skills.Add(new SkillRating(skill, 2, 0.8f, 30, ""));
        var balancedPlan = CoachPlan.Build(balancedProfile);
        Check(!balancedPlan.IsOnboarding && !balancedPlan.IsProvisional && balancedPlan.Steps.Count == 4 && balancedPlan.TotalRuns == 4,
            "recommended balanced veteran gets varied practice, not new-player onboarding");

        var measuredUnder = new SensBlock(.225f, 4, 2, DateTime.Now.AddDays(-2), DateTime.Now, 24, 16, 0,
            .20f, .80f, 6, 1.7f, 1.1f, 0, 0, .07f, .75f, SensVerdict.Undershoot);
        var underCorrection = UI.SensLabScreen.CorrectivePlan(measuredUnder);
        Check(underCorrection is { Steps.Count: >= 2 and <= 3 } && underCorrection.Steps.All(s =>
                Modes.ModeRegistry.Find(s.Drill)?.Invoke().Key == s.Drill && s.Runs > 0
                && s.Source != null && s.Source.Fixes.Length > 0 && !string.IsNullOrWhiteSpace(s.Source.Fixes[0])
                && s.Recoil == CoachPlan.RecoilFor(s.Drill).Advice),
            "sensitivity correction: undershoot has resolvable steps, repetitions, cues and matching recoil");
        foreach (var kind in new[] { SensVerdict.Overshoot, SensVerdict.Mixed, SensVerdict.Shaky, SensVerdict.Tracking })
        {
            var correction = UI.SensLabScreen.CorrectivePlan(measuredUnder with { Verdict = kind });
            Check(correction is { Steps.Count: >= 2 and <= 3 } && correction.Steps.All(s =>
                    Modes.ModeRegistry.Find(s.Drill)?.Invoke().Key == s.Drill && s.Source?.Fixes.Length > 0),
                $"sensitivity correction: {kind} has a playable routine");
        }
        Check(UI.SensLabScreen.CorrectivePlan(measuredUnder with { Verdict = SensVerdict.Balanced }) == null,
            "sensitivity correction: balanced measurements never create a routine");
        Check(UI.SensLabScreen.CorrectivePlan(measuredUnder with { Flicks = 4, OffTarget = 2 }) == null,
            "sensitivity correction: insufficient measurements never create a routine");

        Check(Game.Bots.BotBrain.TurnDegPerSec(Difficulty.Get(4).Bot) > Game.Bots.BotBrain.TurnDegPerSec(Difficulty.Get(0).Bot),
            "bot aim turn speed follows the difficulty skill instead of snapping every tier equally");
        Check(Game.Bots.BotBrain.SoundErrorRadius(0, 30) > Game.Bots.BotBrain.SoundErrorRadius(0, 5)
            && Game.Bots.BotBrain.SoundErrorRadius(5, 30) < Game.Bots.BotBrain.SoundErrorRadius(0, 30),
            "bot hearing uncertainty grows with distance and stays nonzero at Nightmare");
        var heardSource = new Vector3(12, 1, -8);
        var heard = Game.Bots.BotBrain.SoundPosition(heardSource, Vector3.Zero, 2, new Random(19));
        float heardRadius = Game.Bots.BotBrain.SoundErrorRadius(2, heardSource.DistanceTo(Vector3.Zero));
        Check(Mathf.Abs(heard.Y - heardSource.Y) < .0001f && new Vector2(heard.X - heardSource.X, heard.Z - heardSource.Z).Length() <= heardRadius + .0001f,
            "bot sound estimates stay in the horizontal uncertainty disk");
        Check(Game.Bots.BotBrain.SoundPosition(heardSource, Vector3.Zero, 2, new Random(19)) == heard,
            "bot sound uncertainty is deterministic with the match RNG");
        Check(Modes.TacticalSite.StableSeed("lotus") == Modes.TacticalSite.StableSeed("lotus")
            && Modes.TacticalSite.StableSeed("lotus") != Modes.TacticalSite.StableSeed("ascent"),
            "tactical layout seeds use a stable text hash rather than process-randomized String.GetHashCode");

        Check(CaValAim.Maps.MapSpots.All.All(m => World.Themes.For(m).Key == m.Key),
            "every map keeps its own lighting preset");
        CollisionChecks();
        // ---- coach-built drills (CoachForge) ----
        Diagnosis Finding(string id, Skill skill) => new(id, skill, Severity.Major, "", "", "", Array.Empty<string>(), Array.Empty<string>(), 30);
        var small = new Dictionary<string, float> { ["flick.n_still"] = 40, ["flick.mean_overshoot_deg"] = 0.6f, ["flick.amp_deg"] = 18, ["flick.r_deg"] = 0.57f };
        var large = new Dictionary<string, float>(small) { ["flick.mean_overshoot_deg"] = 1.4f };
        string? builtSmall = PersonalDrills.For(Finding("OVERFLICK", Skill.Flicking), small);
        string? builtLarge = PersonalDrills.For(Finding("OVERFLICK", Skill.Flicking), large);
        var variantSmall = builtSmall == null ? null : PersonalDrills.Resolve(builtSmall[(builtSmall.IndexOf(':') + 1)..]);
        var variantLarge = builtLarge == null ? null : PersonalDrills.Resolve(builtLarge[(builtLarge.IndexOf(':') + 1)..]);
        Check(builtSmall != null && builtSmall.Contains(CoachForge.Prefix) && builtSmall == PersonalDrills.For(Finding("OVERFLICK", Skill.Flicking), small)
            && Modes.ModeRegistry.Find(builtSmall)?.Invoke().Key == builtSmall,
            "coach builds a reproducible, playable drill from measured overshoot");
        Check(variantSmall != null && variantLarge != null && variantLarge.Radius > variantSmall.Radius && variantSmall.At(0) == variantSmall,
            "a larger measured error builds a larger target, and the built size ignores the difficulty tier");
        Check(PersonalDrills.For(Finding("OVERFLICK", Skill.Flicking), new Dictionary<string, float> { ["flick.n_still"] = 3 })
            == PersonalDrills.For(Finding("OVERFLICK", Skill.Flicking)),
            "too few measurements fall back to the authored prescription");
        var trackM = new Dictionary<string, float> { ["track.secs"] = 40, ["track.jitter"] = 1.1f, ["track.r_deg"] = 1.2f, ["track.lag_deg"] = 1.5f, ["track.overrun_deg"] = 1.3f };
        foreach (string code in new[] { "JITTER", "TRACK_BEHIND", "TRACK_OVERRUN" })
        {
            string? key = PersonalDrills.For(Finding(code, Skill.Tracking), trackM);
            Check(key != null && key.StartsWith("trackv:" + CoachForge.Prefix, StringComparison.Ordinal) && Modes.ModeRegistry.Find(key)?.Invoke().Key == key,
                "coach builds a tracking drill for " + code);
        }
        Check(CoachForge.Resolve("forge-over-x-30") == null && CoachForge.Resolve("forge-nothing-10") == null && CoachForge.Resolve("forge-over") == null
            && CoachForge.Resolve("forge-over-9999-9999") is { Radius: <= 0.48f, Yaw: <= 60 },
            "malformed built ids are rejected and out-of-range values are clamped");

        // ---- Academy ----
        Check(Academy.All.Select(t => t.Id).Distinct().Count() == Academy.All.Length && Academy.All.All(t =>
                t.StepsEn.Length == t.StepsEs.Length && t.StepsEn.Length >= 4 && t.MistakesEn.Length == t.MistakesEs.Length
                && t.NameEs.Length > 0 && t.SummaryEs.Length > 0 && t.CueEs.Length > 0),
            $"academy: {Academy.All.Length} lessons, each complete in English and Spanish");
        foreach (var lesson in Academy.All)
            Check(lesson.Drills.Length >= 3 && lesson.Drills.All(k => Modes.ModeRegistry.Find(k)?.Invoke().Key == k),
                $"academy lesson {lesson.Id}: every drill resolves");
        Check(Academy.All.SelectMany(t => t.Videos).All(v => v.Url.StartsWith("https://www.youtube.com/watch?v=", StringComparison.Ordinal)
                && v.Url.Length == 43 && v.Author.Length > 0 && v.Title.Length > 0 && v.Language is "en" or "es"),
            "academy: every video is a credited YouTube link");
        string academyCredits = Godot.FileAccess.GetFileAsString("res://CREDITS.md");
        foreach (var video in Academy.All.SelectMany(t => t.Videos))
            Check(academyCredits.Contains(video.Url, StringComparison.Ordinal)
                && academyCredits.Contains(video.Author, StringComparison.OrdinalIgnoreCase),
                $"academy video appears with author and URL in shipped credits: {video.Url}");
        foreach (string code in new[] { "OVERFLICK", "UNDERFLICK", "AIM_TREMOR", "JITTER", "TRACK_BEHIND", "MICRO_CONFIRM", "XHAIR_LOW", "RUN_GUN", "SPRAY_VERT", "SLOW_REACT", "TARGET_ORDER" })
            Check(Academy.For(Finding(code, Skill.Flicking))?.Diagnoses.Contains(code) == true, "academy answers the finding " + code);

        Check(Academy.Find("scenarios") != null && Academy.Find("hands") != null && Academy.Find("warmup") != null
            && Academy.Find("mental") != null, "academy includes scenario selection, hands, warm-up and mental preparation");
        Check(Academy.Find("hands")!.PreparationGuideId == "hands" && Academy.Find("mental")!.PreparationGuideId == "mind"
            && Academy.Find("warmup")!.PreparationGuideId == "full", "academy lessons open their matching guided preparation");
        Check(Academy.Find("tension")!.StepsEn.Any(s => s.Contains("Palm", StringComparison.Ordinal)
                && s.Contains("Claw", StringComparison.Ordinal) && s.Contains("Fingertip", StringComparison.Ordinal)),
            "grip lesson explains palm, claw and fingertip without declaring a universal winner");
        Check(Academy.All.SelectMany(t => t.Research).All(r => r.Url.StartsWith("https://", StringComparison.Ordinal)
                && r.Title.Length > 0 && r.Author.Length > 0 && r.Language is "en" or "es"),
            "academy research references have an author, title, language and HTTPS URL");
        foreach (var source in Academy.All.SelectMany(t => t.Research))
            Check(academyCredits.Contains(source.Url, StringComparison.Ordinal)
                && academyCredits.Contains(source.Author, StringComparison.OrdinalIgnoreCase),
                $"academy research appears with author and URL in shipped credits: {source.Url}");
        Check(PreparationGuide.All.Select(p => p.Id).Distinct().Count() == 3
            && PreparationGuide.All.All(p => p.Steps.All(s => s.Seconds > 0 && s.TitleEn.Length > 0 && s.TitleEs.Length > 0
                && s.DetailEn.Length > 0 && s.DetailEs.Length > 0 && s.SourceUrl.StartsWith("https://", StringComparison.Ordinal))),
            "preparation: hands, mind and full guides have unique IDs and complete credited bilingual steps");
        Check(PreparationGuide.Get("hands").TotalSeconds == 180 && PreparationGuide.Get("mind").TotalSeconds == 120
            && PreparationGuide.Get("full").TotalSeconds == 300, "preparation: hands 3 min, mind 2 min, full 5 min");
        var prep = new PreparationSession(PreparationGuide.Get("hands"));
        Check(prep.Index == 0 && prep.Paused && prep.Remaining == 15, "preparation starts paused with the first posture step ready");
        prep.TogglePause(); prep.Advance(15);
        Check(prep.Index == 1 && prep.Remaining == 15, "preparation advances exactly to shoulder mobility");
        prep.TogglePause(); prep.Advance(30);
        Check(prep.Index == 1 && prep.Remaining == 15, "preparation pause freezes the active step");
        prep.Next();
        Check(prep.Index == 2 && prep.Remaining == 20 && !prep.Paused, "preparation next skips only the current step");
        prep.Advance(1000);
        Check(prep.Done && prep.Remaining == 0, "preparation finishes at zero without negative time");
        prep.Reset();
        Check(prep.Index == 0 && prep.Paused && prep.Remaining == 15 && !prep.Done, "preparation restart restores the initial paused state");
        foreach (var source in PreparationGuide.All.SelectMany(p => p.Steps).Select(s => s.SourceUrl).Distinct())
            Check(academyCredits.Contains(source, StringComparison.Ordinal), "preparation source is included in shipped credits: " + source);

        foreach (var (id, minutes) in new[] { ("flick-focus-10", 10f), ("flick-to-duel-20", 20f), ("complete-aim-30", 30f) })
        {
            var routine = RoutineLibrary.Find(id);
            Check(routine != null && routine.Playable && routine.Minutes == minutes && routine.Steps.First().Drill == routine.Steps.Last().Drill
                && routine.Steps.All(s => s.HowEn.Length > 0 && s.HowEs.Length > 0 && s.WhyEn.Length > 0 && s.WhyEs.Length > 0),
                $"editorial routine {id}: {minutes} minutes, playable steps and matched baseline/validation");
            Check(routine != null && routine.CreditEn.Contains("editorial", StringComparison.OrdinalIgnoreCase)
                && routine.CreditEs.Contains("editorial", StringComparison.OrdinalIgnoreCase),
                $"editorial routine {id}: no universal-best or scientifically optimal claim");
        }

        var editorial20 = RoutineLibrary.Find("flick-to-duel-20")!;
        Check(editorial20.DescriptionEn.Contains("stop-shoot", StringComparison.Ordinal) && !editorial20.DescriptionEn.Contains("peek duels", StringComparison.Ordinal)
            && !editorial20.DescriptionEs.Contains("peek duels", StringComparison.Ordinal), "20-minute routine description names only included transfer activities");

        var warmup = new CaValAim.Warmup.WarmupScreen();
        warmup.OpenSources();
        Check(Main.I.GetChildren().OfType<UI.AcademyScreen>().LastOrDefault()?.BackAction == Main.I.ShowLockIn,
            "Lock-In sources return to Lock-In instead of the main menu");
        Action expectedBack = () => { };
        var academyLesson = new UI.AcademyScreen("hands", expectedBack);
        academyLesson.OpenPreparation("hands");
        var preparation = Main.I.GetChildren().OfType<UI.PreparationScreen>().LastOrDefault();
        Check(preparation != null, "Academy opens its matching preparation screen");
        preparation!.BackAction!.Invoke();
        Check(Main.I.GetChildren().OfType<UI.AcademyScreen>().LastOrDefault()?.BackAction == expectedBack,
            "Academy preparation preserves the original coach/practice return action");

        var reactionRun = new RunTelemetry { Mode = "reaction", Duration = 1 };
        reactionRun.Events.Add(new TelemetryEvent(0.8f, "reaction", 420));
        reactionRun.Shots.Add(new ShotSample { T = 0.8f, Accurate = false, Speed = 2 });
        var observations = CoachObservations.Build(reactionRun);
        Check(observations.Count == 2 && observations.Any(o => o.Code == "reaction_ms" && o.Value == 420) &&
            observations.Any(o => o.Code == "inaccurate_shot"), "independent observations preserve concurrent findings");
        var archive = CoachArchive.From(reactionRun, new RunReview { Observations = observations,
            PrescribedDrills = new[] { prescription! } }, null);
        var jsonOptions = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var restored = System.Text.Json.JsonSerializer.Deserialize<CoachArchive>(
            System.Text.Json.JsonSerializer.Serialize(archive, jsonOptions), jsonOptions);
        Check(restored?.Review.Observations.Count == 2 && restored.Review.PrescribedDrills[0] == prescription,
            "coach observations and generated prescriptions survive JSON round trip");
        var normalView = new PlayerView();
        var targetAngle = new Vector2(8, 5);
        var beforeScreen = PlayerView.ScreenOffset(targetAngle, 10);
        normalView.Look(new Vector2(30, -20), 1, 1);
        var afterScreen = PlayerView.ScreenOffset(targetAngle - new Vector2(normalView.ViewYaw, normalView.ViewPitch), 10);
        Check(beforeScreen.X > 0 && beforeScreen.Y < 0 && afterScreen.Length() < beforeScreen.Length(),
            "reaction aiming uses normal mouse directions: moving up/right approaches an upper-right target");
        normalView.Look(new Vector2(-30, 20), 1, 1);
        Check(normalView.ViewYaw == 0 && normalView.ViewPitch == 0,
            "reaction projection does not alter raw mouse sensitivity or direction");
        var reactionGames = Modes.ReactionGameMode.Factories().Select(f => (Modes.ReactionGameMode)f()).ToArray();
        Check(reactionGames.Length == 50 && reactionGames.Select(g => g.Key).Distinct().Count() == 50 &&
            reactionGames.Count(g => !g.NeedsAim) == 35 && reactionGames.All(g => Modes.ModeRegistry.Find(g.Key)?.Invoke().Key == g.Key),
            "50 reaction games resolve, 35 without aiming and 15 with aiming");
        Check(Modes.ReactionGameMode.ShouldGo(Modes.ReactionGameKind.Memory, 3, 3, false, 0) &&
            !Modes.ReactionGameMode.ShouldGo(Modes.ReactionGameKind.Memory, 2, 3, false, 0) &&
            Modes.ReactionGameMode.ShouldGo(Modes.ReactionGameKind.Rules, 2, 1, false, 0) &&
            !Modes.ReactionGameMode.ShouldGo(Modes.ReactionGameKind.Rules, 2, 1, false, 1) &&
            !Modes.ReactionGameMode.ShouldGo(Modes.ReactionGameKind.Parity, 3, 1, true, 0),
            "memory, parity and rule-switch decisions follow their instructions");
        Check(Math.Abs(85 + Modes.ReactionGameMode.ReachablePitchOffset(85, 20) - 65) < 0.001f &&
            Math.Abs(-85 + Modes.ReactionGameMode.ReachablePitchOffset(-85, -20) + 65) < 0.001f,
            "reaction aim targets stay reachable at both pitch limits");
        var gameRun = new RunTelemetry { Mode = "reactgame:rules-3", Duration = 4 };
        gameRun.Events.Add(new TelemetryEvent(1, "reaction_game", 450));
        gameRun.Events.Add(new TelemetryEvent(2, "reaction_game_false_alarm"));
        gameRun.Events.Add(new TelemetryEvent(2, "reaction_game_trial", 0));
        gameRun.Events.Add(new TelemetryEvent(3, "reaction_game_trial", 1));
        var gameReview = Coach.ReviewRun(gameRun, Coach.Summarize(gameRun), 1);
        Check(gameReview.Metrics.GetValueOrDefault("game.response_ms") == 450 && !gameReview.Metrics.ContainsKey("react.ms") &&
            gameReview.Plays?.Plays.Count == 2 && gameReview.Timeline?.Seconds.Count >= 4 && gameReview.Observations.Count == 2 && gameReview.Issues.Any(d => d.Id == "REACTION_GAME_RULE") && gameReview.PrescribedDrills.Single() == "reactgame:rules-2" &&
            CoachPlan.FromRun(gameReview).Steps[0].Drill == "reactgame:rules-2",
            "game coach records decision errors and prescribes an easier matching game without simple-reaction rank norms");
        foreach (var (name, make) in new (string, Action<VmMesh>)[]
        {
            ("box", m => m.Box(Vector3.One, Transform3D.Identity)),
            ("beveled receiver", m => m.ChamferZ(-1, 1, Vector2.One, Vector2.One, 0, 0, 0.2f)),
            ("descending magazine", m => m.ChamferPathY(new[] { (1f, 1f, 1f, 0f), (-1f, 1f, 1f, 0f) }, 0.2f, Transform3D.Identity)),
        })
        {
            var source = new VmMesh();
            make(source);
            using var mesh = new ArrayMesh();
            using var material = new StandardMaterial3D();
            source.AddTo(mesh, material);
            var arrays = mesh.SurfaceGetArrays(0);
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            bool exterior = indices.Length > 0;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                var face = (vertices[b] - vertices[a]).Cross(vertices[c] - vertices[a]);
                exterior &= face.Dot(vertices[a] + vertices[b] + vertices[c]) < 0
                    && normals[a].Dot(vertices[a]) > 0;
            }
            Check(exterior, $"{name} faces are visible from outside");
        }
        ProceduralMeshChecks(report);
        foreach (bool indexed in new[] { false, true })
        {
            var world = new List<Box>
            {
                new(new Vector3(1, 0, -5), new Vector3(1.03f, 3, 5)),
            };
            if (indexed)
                for (int i = 0; i < 40; i++)
                    world.Add(new Box(new Vector3(20 + i, 0, 20), new Vector3(20.1f + i, 3, 21)));
            var feet = Vector3.Zero;
            var velocity = new Vector3(30, 0, 2);
            Collision.MoveAndSlide(ref feet, ref velocity, new Vector3(4, 0, 1), world);
            Check(Mathf.Abs(feet.X - (1 - Collision.PlayerRadius)) < 0.001f
                && Mathf.Abs(feet.Z - 1) < 0.001f && velocity.X == 0 && velocity.Z == 2,
                $"swept thin wall, slide preserved (grid={indexed})");
            feet = new Vector3(4, 0, 0); velocity = new Vector3(-30, 0, 0);
            Collision.MoveAndSlide(ref feet, ref velocity, new Vector3(-5, 0, 0), world);
            Check(Mathf.Abs(feet.X - (1.03f + Collision.PlayerRadius)) < 0.001f,
                $"reverse sweep stops on near face (grid={indexed})");
        }
        var ceiling = new List<Box> { new(new Vector3(-5, 1.4f, -5), new Vector3(5, 2, 5)) };
        var crouched = Vector3.Zero; var vel = new Vector3(2, 0, 0);
        Collision.MoveAndSlide(ref crouched, ref vel, new Vector3(1, 0, 0), ceiling, 0, MovementTuning.CrouchHeight);
        Check(crouched.X == 1 && Collision.Ceiling(crouched, ceiling, 1.2f) == 1.4f,
            "crouched passage preserves low ceiling");
        var keys = new CaValAim.Valorant.ValorantProfile();
        var oldInput = Mover.KeyOverride;
        try
        {
            bool crouchHeld = true;
            Mover.KeyOverride = key => crouchHeld && key == keys.Binds.Primary(CaValAim.Valorant.GameAction.Crouch);
            var floor = new Box(new Vector3(-10, -1, -10), new Vector3(10, 0, 10));
            var tunnel = new List<Box>(ceiling) { floor };
            var mover = new Mover();
            var feet = Vector3.Zero;
            mover.Place(ref feet, tunnel);
            mover.Step(1f / 60, 0, keys, ref feet, tunnel);
            crouchHeld = false;
            mover.Step(1f / 60, 0, keys, ref feet, tunnel);
            Check(mover.BodyCrouched && mover.Crouching && mover.BodyHeight == MovementTuning.CrouchHeight,
                "releasing crouch under a roof keeps both body and movement crouched");
            mover.Step(1f / 60, 0, keys, ref feet, new[] { floor });
            Check(!mover.BodyCrouched && !mover.Crouching,
                "leaving the low roof restores standing automatically");
        }
        finally { Mover.KeyOverride = oldInput; }
        var stair = new List<Box> { new(new Vector3(-2, 2, -2), new Vector3(2, 3, 2)) };
        Check(Mathf.Abs(Collision.HeadroomStep(Vector3.Zero, stair, 0.7f, 1.8f) - 0.2f) < 0.001f,
            "step height respects headroom");
        float RecoverAt(int fps)
        {
            float h = 4;
            for (int i = 0; i < fps / 2; i++) h = WeaponController.Recover(h, 1f / fps, 0.375f);
            return h;
        }
        Check(Mathf.Abs(RecoverAt(30) - RecoverAt(144)) < 0.0001f
            && Mathf.Abs(RecoverAt(30) - 4f / 9f) < 0.0001f,
            "recoil recovery agrees at 30 and 144 FPS");
        var health = new Modes.ContactHealth();
        health.Tick(true, 0.25f, 0.5f, 0.3f, 1f);
        health.Tick(false, 0.2f, 0.5f, 0.3f, 1f);
        Check(Mathf.Abs(health.Progress - 0.25f) < 0.0001f, "regen preserves damage during grace period");
        health.Tick(false, 0.3f, 0.5f, 0.3f, 1f);
        Check(Mathf.Abs(health.Progress - 0.15f) < 0.0001f, "regen heals only the part of a frame beyond grace");
        health.Tick(true, 0.35f, 0.5f, 0.3f, 1f);
        Check(Mathf.Abs(health.Progress - 0.5f) < 0.0001f, "reacquiring finishes remaining target health");
        health.Reset();
        Check(health.Progress == 0, "respawn resets target health");
        float previous = 1;
        bool monotonic = true;
        for (int distance = 0; distance <= 45; distance++)
        {
            float gain = Sfx.DistanceGain(distance, 40, 0.45f);
            monotonic &= gain >= 0 && gain <= previous;
            previous = gain;
        }
        Check(monotonic && Sfx.DistanceGain(40, 40, 0.45f) == 0,
            "spatial audio fades monotonically to silence");
    }

    /// <summary>Structural and rendering-safety invariants for every self-authored first-person mesh.</summary>
    static void ProceduralMeshChecks(Action<string> report)
    {
        void Check(bool ok, string label) => report($"  {(ok ? "PASS" : "FAIL")} gameplay: {label}");
        static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);

        var kinds = Enum.GetValues<WeaponKind>()
            .Where(k => k is WeaponKind.Vandal or WeaponKind.Phantom || ProceduralArsenal.Has(k)).ToArray();
        var rifleGeometry = new Dictionary<WeaponKind, HashSet<Vector3I>>();
        int totalTriangles = 0;
        foreach (var kind in kinds)
        {
            Node3D? root = null;
            try
            {
                root = kind is WeaponKind.Vandal or WeaponKind.Phantom
                    ? ProceduralRifle.Build(kind) : ProceduralArsenal.Build(kind);
                var muzzle = root.GetNodeOrNull<Node3D>("Muzzle");
                var magazine = root.GetNodeOrNull<Node3D>("Magazine");
                Check(muzzle != null && magazine != null && Finite(muzzle.Position),
                    $"procedural {kind}: Muzzle and Magazine anchors exist and are finite");

                bool valid = true;
                int meshCount = 0, triangles = 0;
                var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                var geometry = new HashSet<Vector3I>();

                void Visit(Node node, Transform3D parent)
                {
                    var transform = node is Node3D spatial ? parent * spatial.Transform : parent;
                    if (node is MeshInstance3D mi && mi.Mesh != null)
                    {
                        meshCount++;
                        for (int surface = 0; surface < mi.Mesh.GetSurfaceCount(); surface++)
                        {
                            var arrays = mi.Mesh.SurfaceGetArrays(surface);
                            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                            var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
                            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                            valid &= vertices.Length > 0 && normals.Length == vertices.Length && indices.Length >= 3 && indices.Length % 3 == 0;
                            triangles += indices.Length / 3;
                            for (int i = 0; i < vertices.Length; i++)
                            {
                                var vertex = vertices[i];
                                var normal = normals.Length == vertices.Length ? normals[i] : Vector3.Zero;
                                valid &= Finite(vertex) && Finite(normal) && Mathf.Abs(normal.LengthSquared() - 1f) < 0.004f;
                                if (!Finite(vertex)) continue;
                                var point = transform * vertex;
                                valid &= Finite(point);
                                min = new Vector3(Mathf.Min(min.X, point.X), Mathf.Min(min.Y, point.Y), Mathf.Min(min.Z, point.Z));
                                max = new Vector3(Mathf.Max(max.X, point.X), Mathf.Max(max.Y, point.Y), Mathf.Max(max.Z, point.Z));
                                // Quantisation makes this independent of vertex order, material slots and exact counts.
                                geometry.Add(new Vector3I(Mathf.RoundToInt(point.X * 100000f),
                                    Mathf.RoundToInt(point.Y * 100000f), Mathf.RoundToInt(point.Z * 100000f)));
                            }
                            for (int i = 0; i < indices.Length; i++)
                                valid &= indices[i] >= 0 && indices[i] < vertices.Length;
                        }
                    }
                    foreach (var child in node.GetChildren()) Visit(child, transform);
                }
                Visit(root, Transform3D.Identity);

                var size = max - min;
                bool bounds = meshCount > 0 && Finite(min) && Finite(max) && Finite(size) &&
                    size.X > 0.002f && size.Y > 0.002f && size.Z > 0.002f && size.Length() < 2f &&
                    min.Length() < 2f && max.Length() < 2f;
                Check(valid && bounds && triangles > 0 && triangles <= 24000,
                    $"procedural {kind}: {triangles} triangles within budget; finite vertices, unit normals, valid indices and sane bounds ({size.X:0.00}/{size.Y:0.00}/{size.Z:0.00} m)");
                totalTriangles += triangles;
                if (kind is WeaponKind.Vandal or WeaponKind.Phantom) rifleGeometry[kind] = geometry;
            }
            finally
            {
                root?.Free();
            }
        }
        Check(totalTriangles > 0 && totalTriangles <= 240000,
            $"procedural arsenal total rendering budget: {totalTriangles} triangles across {kinds.Length} models");

        var vandal = rifleGeometry.GetValueOrDefault(WeaponKind.Vandal);
        var phantom = rifleGeometry.GetValueOrDefault(WeaponKind.Phantom);
        int shared = vandal != null && phantom != null ? vandal.Intersect(phantom).Count() : 0;
        int union = vandal != null && phantom != null ? vandal.Count + phantom.Count - shared : 0;
        float distinct = union > 0 ? 1f - shared / (float)union : 0f;
        Check(union > 0 && distinct > 0.05f,
            $"Vandal and Phantom retain genuinely distinct mesh geometry ({distinct:P0} non-shared quantised vertices)");
    }

    /// <summary>
    /// The collision rules the bots and the player share: what blocks a body, what only blocks a bullet, and what the
    /// nav grid may plan through. These are the cases behind "the bots get stuck" and "there are invisible walls".
    /// </summary>
    static void CollisionChecks()
    {
        void Check(bool ok, string label) => Console.WriteLine($"  {(ok ? "PASS" : "FAIL")} collision: {label}");

        float fallHigh = GameSession.FallLimit(0, null, new Vector3(0, 8, 0));
        float fallBelow = GameSession.FallLimit(0, null, new Vector3(0, -30, 0));
        Check(fallHigh == -12 && fallBelow == -50,
            $"fall guard without a safe floor uses a fixed spawn/map threshold ({fallHigh:0.###}/{fallBelow:0.###}, expected -12/-50)");
        Check(GameSession.FallLimit(0, new Vector3(0, 12, 0), new Vector3(0, 8, 0)) == -8 &&
            GameSession.FallLimit(-40, null, new Vector3(0, 8, 0)) == -44,
            "fall guard respects both the last safe floor and the map underside");

        // A ray that starts on a wall and points away from it does not cross the wall: it must not report a hit.
        var wall = new List<Box> { new(new Vector3(-1, 0, -1), new Vector3(1, 3, 1)) };
        Check(!Collision.Blocked(new Vector3(1, 1.5f, 0), new Vector3(3, 1.5f, 0), wall),
            "a ray leaving a wall through its face is not blocked");
        Check(Collision.Blocked(new Vector3(-3, 1.5f, 0), new Vector3(3, 1.5f, 0), wall),
            "a ray crossing a wall is blocked");
        Check(!Collision.Blocked(new Vector3(-3, 1.5f, 0), new Vector3(3, 1.5f, 0), new List<Box>()),
            "an empty world blocks nothing");

        // The nav grid must not plan a straight line through a gap the body does not fit through. The old test cast a
        // ray at knee height, so a ledge at head height was invisible to it and the bot pushed into it forever.
        var floor = new Box(new Vector3(-20, -0.5f, -20), new Vector3(20, 0, 20));
        var ledge = new Box(new Vector3(-20, 1.2f, -20), new Vector3(20, 1.4f, 20)); // 1.2 m up: the body does not fit
        var navWorld = new List<Box> { floor, ledge };
        var nav = CaValAim.Game.Bots.BotNav.For("collision-check:overhang", navWorld, Vector3.Zero);
        bool straightUnderLedge = nav.Straight(new Vector3(-4, 0, 0), new Vector3(4, 0, 0));
        Check(!straightUnderLedge,
            "the nav does not plan through a gap the body does not fit through");
        // A low obstacle is stepped over, so the line stays walkable.
        var step = new Box(new Vector3(-20, 0, -20), new Vector3(20, 0.3f, 20));
        var stepNav = CaValAim.Game.Bots.BotNav.For("collision-check:step", new List<Box> { floor, step }, Vector3.Zero);
        Check(stepNav.Straight(new Vector3(-4, 0, 0), new Vector3(4, 0, 0)),
            "the nav walks over an obstacle it can step onto");
        // An open corridor is walkable.
        var openNav = CaValAim.Game.Bots.BotNav.For("collision-check:open", new List<Box> { floor }, Vector3.Zero);
        Check(openNav.Straight(new Vector3(-4, 0, 0), new Vector3(4, 0, 0)),
            "the nav walks through open ground");

        var sealedNav = CaValAim.Game.Bots.BotNav.For("collision-check:sealed",
            new List<Box> { new(new Vector3(-2, -1, -2), new Vector3(2, 31, 2)) }, Vector3.Zero);
        Check(sealedNav.Count == 0,
            "a world without standing room produces an empty nav grid without crashing");
        Check(sealedNav.Random(new Random(7)) == Vector3.Zero,
            "an empty nav grid falls back to its seed instead of indexing an empty node array");

        var dividedWorld = new List<Box>
        {
            new(new Vector3(-4, -.5f, -4), new Vector3(4, 0, 4)),
            new(new Vector3(1, 0, -4), new Vector3(1.2f, 3, 4)),
        };
        var dividedNav = CaValAim.Game.Bots.BotNav.For("collision-check:reachable-unwedge", dividedWorld, Vector3.Zero);
        int reachable = dividedNav.NearestReachable(Vector3.Zero);
        Check(reachable >= 0 && dividedNav.Pos[reachable].X < 1,
            "un-wedging chooses a reachable node rather than the geometrically closest point through a wall");

        // Shared broadphase stamps must remain correct while navigation is built on workers and the main thread
        // continues LOS/body queries. Use >32 boxes so this exercises the grid rather than the linear fallback.
        var concurrentWorld = new List<Box> { floor };
        for (int i = 0; i < 40; i++)
            concurrentWorld.Add(new Box(new Vector3(i * 3, 0, 5), new Vector3(i * 3 + 1, 3, 6)));
        int concurrentFailures = 0;
        Parallel.For(0, 2000, _ =>
        {
            bool ok = Collision.Ground(Vector3.Zero, concurrentWorld) == 0
                && !Collision.Overlaps(Vector3.Zero, concurrentWorld)
                && Collision.Overlaps(new Vector3(.5f, 0, 5.5f), concurrentWorld)
                && Collision.Blocked(new Vector3(-2, 1.5f, 5.5f), new Vector3(120, 1.5f, 5.5f), concurrentWorld);
            var near = new List<int>();
            Collision.Near(concurrentWorld, -1, -1, 1, 1, near);
            ok &= near.Count > 0 && near.Count == near.Distinct().Count();
            if (!ok) Interlocked.Increment(ref concurrentFailures);
        });
        Check(concurrentFailures == 0, $"parallel collision queries keep independent query stamps ({concurrentFailures} failures)");

        // A spawn in the middle of a wall must be moved to a standable spot, never left inside the geometry.
        var solid = new List<Box> { floor, new Box(new Vector3(-1, 0, -1), new Vector3(1, 3, 1)) };
        bool inside = Collision.Overlaps(Vector3.Zero, solid, Collision.PlayerRadius);
        var resolved = SpawnSafety.Resolve(Vector3.Zero, solid, out bool ok);
        Check(inside && ok && !Collision.Overlaps(resolved, solid, Collision.PlayerRadius),
            "a spawn inside a wall is moved to a standable spot");
    }
}

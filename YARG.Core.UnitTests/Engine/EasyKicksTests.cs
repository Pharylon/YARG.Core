using System.Collections.Generic;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Game;
using YARG.Core.Input;

namespace YARG.Core.UnitTests.Engine;

// The Easy Kicks modifier widens the hit window of kick notes by half (a quarter of the window
// on each end) and leaves every other note alone. The default drums window is 140ms, so a
// normal note can be hit up to 70ms off, and an easy kick up to 105ms off.
public class EasyKicksTests : EngineTester
{
    private const double KICK_TIME = 2.0;

    // Inside the easy kick window, outside the normal one
    private const double WIDE_ONLY_OFFSET = 0.09;

    // Outside both
    private const double OUTSIDE_OFFSET = 0.12;

    private static DrumNote Note(FourLaneDrumPad pad, double time)
    {
        return new DrumNote(pad, DrumNoteType.Neutral, DrumNoteFlags.None, NoteFlags.None,
            time, (uint) (time * 960));
    }

    private const double TAIL_TIME = KICK_TIME + 1.5;

    // The given notes, between a lead-in red note at 0 and a green tail note (overhits only count
    // between the first and the last note of a song). Notes sharing a time must be passed as one chord.
    private static YargDrumsEngine CreateEngine(bool easyKicks, params DrumNote[] notes)
    {
        var all = new List<DrumNote> { Note(FourLaneDrumPad.RedDrum, 0) };
        all.AddRange(notes);
        all.Add(Note(FourLaneDrumPad.GreenDrum, TAIL_TIME));

        for (int i = 1; i < all.Count; i++)
        {
            all[i].PreviousNote = all[i - 1];
            all[i - 1].NextNote = all[i];
        }

        var difficulty = new InstrumentDifficulty<DrumNote>(Instrument.FourLaneDrums, Difficulty.Expert,
            all, new List<Phrase>(), new());

        var syncTrack = new SyncTrack(480);
        syncTrack.Tempos.Add(new TempoChange(120, 0, 0));

        var parameters = EnginePreset.Default.Drums.Create(StarMultiplierThresholds,
            SoloBonusStarMultiplierThresholds, DrumsEngineParameters.DrumMode.NonProFourLane, easyKicks);

        return new YargDrumsEngine(difficulty, syncTrack, parameters, isBot: false, isMidiDrumsInput: false);
    }

    private static DrumNote Chord(double time, params FourLaneDrumPad[] pads)
    {
        var parent = Note(pads[0], time);
        for (int i = 1; i < pads.Length; i++)
        {
            parent.AddChildNote(Note(pads[i], time));
        }

        return parent;
    }

    // Plays the given inputs, plus the lead-in and tail notes, and lets the song run out.
    private static void Run(YargDrumsEngine engine, params (double Time, DrumsAction Action)[] inputs)
    {
        var leadIn = GameInput.Create(0, DrumsAction.RedDrum, 1f);
        engine.QueueInput(ref leadIn);

        foreach (var (time, action) in inputs)
        {
            var input = GameInput.Create(time, action, 1f);
            engine.QueueInput(ref input);
        }

        var tail = GameInput.Create(TAIL_TIME, DrumsAction.GreenDrum, 1f);
        engine.QueueInput(ref tail);

        // Coarse steps on purpose: the engine has to judge on the window edges by itself
        for (double time = 0; time <= TAIL_TIME + 1; time += 0.25)
        {
            engine.Update(time);
        }
    }

    [Test]
    public void Parameters_OnlyWidenKicksWhenTheModifierIsOn()
    {
        var off = EnginePreset.Default.Drums.Create(StarMultiplierThresholds,
            SoloBonusStarMultiplierThresholds, DrumsEngineParameters.DrumMode.NonProFourLane);
        var on = EnginePreset.Default.Drums.Create(StarMultiplierThresholds,
            SoloBonusStarMultiplierThresholds, DrumsEngineParameters.DrumMode.NonProFourLane, true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(off.KickHitWindowMultiplier, Is.EqualTo(1.0));
            Assert.That(on.KickHitWindowMultiplier, Is.EqualTo(1.5));
        }
    }

    [TestCase(-WIDE_ONLY_OFFSET)]
    [TestCase(WIDE_ONLY_OFFSET)]
    public void Kick_InWidenedPartOfWindow_IsOnlyHitWithEasyKicks(double offset)
    {
        var normalKick = Note(FourLaneDrumPad.Kick, KICK_TIME);
        var normal = CreateEngine(false, normalKick);
        Run(normal, (KICK_TIME + offset, DrumsAction.Kick));

        var easyKick = Note(FourLaneDrumPad.Kick, KICK_TIME);
        var easy = CreateEngine(true, easyKick);
        Run(easy, (KICK_TIME + offset, DrumsAction.Kick));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(normalKick.WasMissed, Is.True);
            Assert.That(normal.EngineStats.Overhits, Is.EqualTo(1));

            Assert.That(easyKick.WasHit, Is.True);
            Assert.That(easy.EngineStats.Overhits, Is.Zero);
            Assert.That(easy.EngineStats.NotesHit, Is.EqualTo(3));
        }
    }

    [TestCase(-OUTSIDE_OFFSET)]
    [TestCase(OUTSIDE_OFFSET)]
    public void Kick_OutsideWidenedWindow_IsStillMissed(double offset)
    {
        var kick = Note(FourLaneDrumPad.Kick, KICK_TIME);
        var engine = CreateEngine(true, kick);
        Run(engine, (KICK_TIME + offset, DrumsAction.Kick));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(kick.WasMissed, Is.True);
            Assert.That(engine.EngineStats.Overhits, Is.EqualTo(1));
        }
    }

    [TestCase(-WIDE_ONLY_OFFSET)]
    [TestCase(WIDE_ONLY_OFFSET)]
    public void Pad_InWidenedPartOfWindow_IsNotHelpedByEasyKicks(double offset)
    {
        var yellow = Note(FourLaneDrumPad.YellowDrum, KICK_TIME);
        var engine = CreateEngine(true, yellow);
        Run(engine, (KICK_TIME + offset, DrumsAction.YellowDrum));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(yellow.WasMissed, Is.True);
            Assert.That(engine.EngineStats.Overhits, Is.EqualTo(1));
        }
    }

    // Both chord orders, since the kick may or may not be the parent note of its chord
    [TestCase(FourLaneDrumPad.Kick, FourLaneDrumPad.YellowDrum)]
    [TestCase(FourLaneDrumPad.YellowDrum, FourLaneDrumPad.Kick)]
    public void LateKick_InChord_IsHitAfterTheRestOfTheChordWasHit(FourLaneDrumPad first, FourLaneDrumPad second)
    {
        var chord = Chord(KICK_TIME, first, second);
        var engine = CreateEngine(true, chord);
        Run(engine,
            (KICK_TIME, DrumsAction.YellowDrum),
            (KICK_TIME + WIDE_ONLY_OFFSET, DrumsAction.Kick));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chord.WasFullyHit(), Is.True);
            Assert.That(engine.EngineStats.Overhits, Is.Zero);
            Assert.That(engine.EngineStats.IsFullCombo, Is.True);
        }
    }

    [TestCase(FourLaneDrumPad.Kick, FourLaneDrumPad.YellowDrum)]
    [TestCase(FourLaneDrumPad.YellowDrum, FourLaneDrumPad.Kick)]
    public void EarlyKick_InChord_IsHitBeforeTheRestOfTheChordOpens(FourLaneDrumPad first, FourLaneDrumPad second)
    {
        var chord = Chord(KICK_TIME, first, second);
        var engine = CreateEngine(true, chord);
        Run(engine,
            (KICK_TIME - WIDE_ONLY_OFFSET, DrumsAction.Kick),
            (KICK_TIME, DrumsAction.YellowDrum));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chord.WasFullyHit(), Is.True);
            Assert.That(engine.EngineStats.Overhits, Is.Zero);
            Assert.That(engine.EngineStats.IsFullCombo, Is.True);
        }
    }

    [TestCase(FourLaneDrumPad.Kick, FourLaneDrumPad.YellowDrum)]
    [TestCase(FourLaneDrumPad.YellowDrum, FourLaneDrumPad.Kick)]
    public void LateKick_InChord_IsHitAfterTheRestOfTheChordWasMissed(FourLaneDrumPad first, FourLaneDrumPad second)
    {
        var chord = Chord(KICK_TIME, first, second);
        var after = Note(FourLaneDrumPad.BlueDrum, KICK_TIME + 0.5);
        var engine = CreateEngine(true, chord, after);
        Run(engine,
            (KICK_TIME + WIDE_ONLY_OFFSET, DrumsAction.Kick),
            (KICK_TIME + 0.5, DrumsAction.BlueDrum));

        using (Assert.EnterMultipleScope())
        {
            foreach (var note in chord.AllNotes)
            {
                bool isKick = note.Pad == (int) FourLaneDrumPad.Kick;
                Assert.That(note.WasHit, Is.EqualTo(isKick));
                Assert.That(note.WasMissed, Is.EqualTo(!isKick));
            }

            // The engine carries on normally once the chord is finished
            Assert.That(after.WasHit, Is.True);
            Assert.That(engine.EngineStats.Overhits, Is.Zero);
            Assert.That(engine.EngineStats.NotesHit, Is.EqualTo(4));
        }
    }

    [Test]
    public void UnhitChord_IsFullyMissed_AndLaterNotesStillPlay()
    {
        var chord = Chord(KICK_TIME, FourLaneDrumPad.YellowDrum, FourLaneDrumPad.Kick);
        var after = Note(FourLaneDrumPad.BlueDrum, KICK_TIME + 0.5);
        var engine = CreateEngine(true, chord, after);
        Run(engine, (KICK_TIME + 0.5, DrumsAction.BlueDrum));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(chord.WasFullyMissed(), Is.True);
            Assert.That(after.WasHit, Is.True);
            Assert.That(engine.EngineStats.NotesHit, Is.EqualTo(3));
            Assert.That(engine.EngineStats.Overhits, Is.Zero);
        }
    }

    // A pad hit while a late kick is still pending belongs to the next note, not to the kick's chord
    [Test]
    public void NextNote_CanBeHit_WhileALateKickIsStillPending()
    {
        var chord = Chord(KICK_TIME, FourLaneDrumPad.YellowDrum, FourLaneDrumPad.Kick);
        var after = Note(FourLaneDrumPad.BlueDrum, KICK_TIME + 0.1);
        var engine = CreateEngine(true, chord, after);
        Run(engine,
            (KICK_TIME, DrumsAction.YellowDrum),
            (KICK_TIME + 0.08, DrumsAction.BlueDrum));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.WasHit, Is.True);
            Assert.That(chord.WasFullyHitOrMissed(), Is.True);
            Assert.That(engine.EngineStats.Overhits, Is.Zero);
            Assert.That(engine.EngineStats.NotesHit, Is.EqualTo(4));
        }
    }

    [Test]
    public void Parameters_SurviveAReplayRoundTrip()
    {
        var original = EnginePreset.Default.Drums.Create(StarMultiplierThresholds,
            SoloBonusStarMultiplierThresholds, DrumsEngineParameters.DrumMode.NonProFourLane, true);

        using var memory = new System.IO.MemoryStream();
        using (var writer = new System.IO.BinaryWriter(memory, System.Text.Encoding.UTF8, true))
        {
            original.Serialize(writer);
        }

        byte[] bytes = memory.ToArray();
        using var buffer = YARG.Core.IO.FixedArray<byte>.Alloc(bytes.Length);
        bytes.CopyTo(buffer.Span);

        var stream = new YARG.Core.IO.FixedArrayStream(buffer);
        var restored = new DrumsEngineParameters(ref stream, YARG.Core.Replays.ReplayIO.REPLAY_VERSIONS.CURRENT);

        Assert.That(restored.KickHitWindowMultiplier, Is.EqualTo(1.5));
    }
}

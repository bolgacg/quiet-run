using QuietRun;

namespace QuietRun.Tests;

/// The ring buffer and the smoothing window, which is measured in seconds, not in readings.
public class WindowTests
{
    private static Sample At(double t, double methodS) => new(t, 100, 100, InstrumentStatus.Running, (long)t, methodS);
    private static Sample At(double t) => At(t, t);

    [Fact]
    public void The_ring_buffer_keeps_its_order_when_it_wraps_and_when_it_grows()
    {
        var b = new RingBuffer<int>(4);
        for (int i = 0; i < 3; i++) b.Push(i);
        b.PopFront();
        b.PopFront();
        for (int i = 3; i < 10; i++) b.Push(i);
        Assert.Equal(Enumerable.Range(2, 8), b);
        Assert.Equal(2, b.First);
        Assert.Equal(9, b.Last);
        Assert.Equal(5, b[3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => b[8]);
    }

    [Fact]
    public void The_smoothing_window_holds_15_seconds_of_readings_at_three_different_rates()
    {
        foreach (var (everyS, readings) in new[] { (1.0, 15), (0.5, 30), (2.0, 8) })
        {
            var w = new SampleWindow(Envelope.SmoothSeconds);
            for (int i = 0; i < 200; i++) w.Push(At(i * everyS));
            Assert.Equal(readings, w.Count);
            Assert.True(w.Ready);
        }
    }

    [Fact]
    public void The_window_is_first_used_once_its_readings_cover_15_seconds_not_after_15_readings()
    {
        // Each reading counts for one average interval: at 1 a second the 15th reading (t = 14 s) covers 15 s,
        // at 2 a second the 30th (t = 14.5 s), at one every 2 s the 8th (t = 14 s, covering 16 s).
        foreach (var (everyS, readyAtS) in new[] { (1.0, 14.0), (0.5, 14.5), (2.0, 14.0) })
        {
            var w = new SampleWindow(Envelope.SmoothSeconds);
            for (int i = 0; ; i++)
            {
                double t = i * everyS;
                w.Push(At(t));
                if (w.Ready) { Assert.Equal(readyAtS, t, 9); break; }
                Assert.True(t < readyAtS, $"every {everyS} s: not ready at {t} s");
            }
        }
    }

    [Fact]
    public void After_a_gap_longer_than_the_window_it_waits_to_refill()
    {
        var w = new SampleWindow(Envelope.SmoothSeconds);
        for (int t = 0; t <= 100; t++) w.Push(At(t));
        w.Push(At(130));
        Assert.False(w.Ready);
        for (int t = 131; t <= 137; t++) w.Push(At(t));
        Assert.False(w.Ready);
        w.Push(At(138));
        Assert.True(w.Ready);
    }

    [Fact]
    public void The_method_position_is_read_at_the_middle_of_the_window_in_time()
    {
        // Readings at 0, 1, 2 and 10 s. The middle in time is 5 s, between the readings at 2 s (method 4 s)
        // and 10 s (method 20 s), so the method position is 4 + 3/8 of 16 = 10 s. Counting readings would give 4.
        var w = new SampleWindow(Envelope.SmoothSeconds);
        foreach (var t in new[] { 0.0, 1, 2, 10 }) w.Push(At(t, 2 * t));
        Assert.Equal(10, w.CentreMethodS(), 9);
    }
}

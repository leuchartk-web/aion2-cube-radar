using System.Buffers.Binary;
using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class OwnPositionBeforeIdentityTests
{
    [Theory]
    [InlineData(0x2136, 16, "MapLoad")]
    [InlineData(0x2336, 6, "Teleport")]
    public void Explicit_own_coordinates_are_available_before_identity_arrives(int opcode, int offset, string source)
    {
        var engine = new RadarEngine(new RadarConfig(), trackPlayers: false);
        var body = new byte[offset + 12];
        if (opcode == 0x2136) BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(4), 1010);
        BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(offset), 10000);
        BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(offset + 4), -20000);
        BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(offset + 8), 500);
        engine.OnFrame(1000, (ushort)opcode, body);
        var snapshot = engine.Snapshot();
        Assert.Null(snapshot.SelfId); // coordinates must not fabricate an identity
        Assert.NotNull(snapshot.Self);
        Assert.Equal((10000d, -20000d, 500d, source), (snapshot.Self.X, snapshot.Self.Y, snapshot.Self.Z, snapshot.Self.Source));
        Assert.Empty(snapshot.Players);
        engine.OnFrame(1200, 0x3336, [42]);
        Assert.Equal(42u, engine.SelfId);
        Assert.Equal(snapshot.Self, engine.Snapshot().Self with { AgeMs = snapshot.Self.AgeMs });
    }

    [Fact]
    public void Invalid_teleport_coordinates_do_not_create_a_position()
    {
        var engine = new RadarEngine(new RadarConfig(), trackPlayers: false);
        engine.OnFrame(1000, 0x2336, new byte[18]);
        Assert.Null(engine.Snapshot().Self);
    }
}

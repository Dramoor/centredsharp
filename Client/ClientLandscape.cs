﻿using CentrED.Network;

namespace CentrED.Client;

public partial class ClientLandscape : BaseLandscape {
    private CentrEDClient _client;

    public ClientLandscape(CentrEDClient client, ushort width, ushort height) : base(width, height) {
        _client = client;
        BlockUnloaded += FreeBlock;
        PacketHandlers.RegisterPacketHandler(0x04, 0, OnBlockPacket);
        PacketHandlers.RegisterPacketHandler(0x06, 8, OnDrawMapPacket);
    }

    protected override Block LoadBlock(ushort x, ushort y) {
        AssertBlockCoords(x, y);
        _client.Send(new RequestBlocksPacket(new BlockCoords(x, y)));
        var blockId = BlockCache.BlockId(x, y);
        var block = BlockCache.Get(blockId);
        while (block == null) {
            Thread.Sleep(1);
            _client.Update();
            block = BlockCache.Get(blockId);
        }

        return block;
    }

    private void FreeBlock(Block block) {
        _client.Send(new FreeBlockPacket(block.LandBlock.X, block.LandBlock.Y));
    }
}
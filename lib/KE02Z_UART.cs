//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.UART
{
    public class KE02Z_UART : UARTBase, IBytePeripheral, IKnownSize
    {
        public KE02Z_UART(IMachine machine) : base(machine)
        {
            baudRateDivValue = 0;
            transmitQueue = new Queue<byte>();

            var registersMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.BaudRateHigh, new ByteRegister(this)
                    .WithTaggedFlag("LBKDIE", 7)
                    .WithTaggedFlag("RXEDGIE", 6)
                    .WithTaggedFlag("SBNS", 5)
                    .WithValueField(0, 5, writeCallback: (_, value) =>
                    {
                        // setting the high bits of the baud rate factor
                        BitHelper.ReplaceBits(ref baudRateDivValue, (uint)value, 5, 8);
                    },name: "SBR")
                },
                {(long)Registers.BaudRateLow, new ByteRegister(this)
                    .WithValueField(0, 8, writeCallback: (_, value) =>
                    {
                        // setting the low bits of the baud rate factor
                        BitHelper.ReplaceBits(ref baudRateDivValue, (uint)value, 8);
                        const uint CLOCK_RATE = 16000000; // TODO: Get system clock rate
                        baudRate = CLOCK_RATE / (baudRateDivValue * 16);
                    },name: "SBR")
                },
                {(long)Registers.Control1, new ByteRegister(this)
                    .WithTaggedFlag("LOOPS", 7)
                    .WithTaggedFlag("UARTSWEI", 6)
                    .WithTaggedFlag("RSRE", 5)
                    .WithTaggedFlag("M", 4)
                    .WithTaggedFlag("WAKE", 3)
                    .WithTaggedFlag("ILT", 2)
                    .WithTaggedFlag("PE", 1)
                    .WithTaggedFlag("PT", 0)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.Control2, new ByteRegister(this)
                    .WithFlag(7, out transmitterIRQEnabled, name: "TIE")
                    .WithFlag(6, out txCompleteIRQEnable, name: "TCIE")
                    .WithFlag(5, out receiverIRQEnabled, name: "RIE")
                    .WithTaggedFlag("ILIE", 4)
                    .WithFlag(3, out transmitterEnabled, name: "TE")
                    .WithFlag(2, out receiverEnabled, name: "RE")
                    .WithTaggedFlag("RWU", 1)
                    .WithTaggedFlag("SBK", 0)
                    .WithWriteCallback((_, __) =>
                    {
                        UpdateInterrupts();
                    })
                },
                {(long)Registers.Status1, new ByteRegister(this)
                    .WithFlag(7, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return transmitQueue.Count <= transmitWatermark;
                    },name: "TDRE")
                    .WithFlag(6, FieldMode.Read, valueProviderCallback: (_) => {
                        return (transmitQueue.Count <= 0); // transmit queue empty?
                    }, name: "TC")
                    .WithFlag(5, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return Count >= receiverWatermark;
                    }, name: "RDRF")
                    .WithTaggedFlag("IDLE", 4)
                    .WithTaggedFlag("OR", 3)
                    .WithTaggedFlag("NF", 2)
                    .WithTaggedFlag("FE", 1)
                },
                {(long)Registers.Status2, new ByteRegister(this)
                    .WithTaggedFlag("LBKDIF", 7)
                    .WithTaggedFlag("RXEDGIF", 6)
                    .WithTaggedFlag("MSBF", 5)
                    .WithTaggedFlag("RXINV", 4)
                    .WithTaggedFlag("RWUID", 3)
                    .WithTaggedFlag("BRK13", 2)
                    .WithTaggedFlag("LBKDE", 1)
                    .WithTaggedFlag("RAF", 0)
                },
                {(long)Registers.Control3, new ByteRegister(this)
                    .WithTaggedFlag("R8", 7)
                    .WithTaggedFlag("T8", 6)
                    .WithTaggedFlag("TXDIR", 5)
                    .WithTaggedFlag("TXINV", 4)
                    .WithFlag(3, out overrunIRQEnable, name: "ORIE")
                    .WithFlag(2, out noiseErrorIRQEnable, name: "NEIE")
                    .WithFlag(1, out framingErrorIRQEnable, name: "FEIE")
                    .WithFlag(0, out parityErrorIRQEnable, name: "PEIE")
                },
                {(long)Registers.Data, new ByteRegister(this)
                   .WithValueField(0, 8,
                    writeCallback: (_, b) =>
                    {
                        if(!transmitterEnabled.Value)
                        {
                            this.Log(LogLevel.Warning, "Transmitter not enabled");
                            return;
                        }
                        transmitQueue.Enqueue((byte)b);
                        TransmitData();
                        UpdateInterrupts();
                    },
                    valueProviderCallback: _ =>
                    {
                        if(!receiverEnabled.Value)
                        {
                            return 0;
                        }
                        if(!TryGetCharacter(out var character))
                        {
                            this.Log(LogLevel.Warning, "Trying to read data from empty receive fifo");
                        }
                        UpdateInterrupts();
                        return character;
                    },
                    name: "D")
                }
            };

            IRQ = new GPIO();
            registers = new ByteRegisterCollection(this, registersMap);
            
            registers.Write((long)Registers.Status1, (byte)0xC0); // Transmitter Idle & Tx buffer empty

        }

        public byte ReadByte(long offset)
        {
            byte b = registers.Read(offset);
            //Console.Write("UART Read: " + b);
            return b;
        }

        public void WriteByte(long offset, byte value)
        {
            registers.Write(offset, value);
        }

        protected override void CharWritten()
        {
            UpdateInterrupts();
        }

        protected override void QueueEmptied()
        {
            // do nothing
        }

        public long Size => 8;
        
        public GPIO IRQ { get; private set; }

        //TODO should be calculated based upon UART clock
        public override uint BaudRate => baudRate;
        public override Bits StopBits => Bits.One;
        public override Parity ParityBit => Parity.Even;
        private uint baudRate;
        private uint parity;
        private uint stopBits;
        private void TransmitData()
        {
            if(transmitQueue.Count < transmitWatermark)
            {
                return;
            }

            while(transmitQueue.Count != 0)
            {
                var b = transmitQueue.Dequeue();
                this.TransmitCharacter((byte)b);
            }
        }

        private void UpdateInterrupts()
        {
            IRQ.Set((transmitterEnabled.Value && transmitterIRQEnabled.Value) || 
                    (receiverEnabled.Value && receiverIRQEnabled.Value && Count >= receiverWatermark));
        }

        public override void Reset()
        {
            lock(innerLock) {
                transmitQueue.Clear();
            }
            base.Reset();
        }

        private uint baudRateDivValue;
        private uint receiverWatermark = 0;
        private uint transmitWatermark = 0;
        private readonly Queue<byte> transmitQueue;
        private readonly ByteRegisterCollection registers;
        //private readonly IValueRegisterField baudRateFineAdjustValue;
        private readonly IFlagRegisterField receiverEnabled;
        private readonly IFlagRegisterField transmitterEnabled;
        private readonly IFlagRegisterField transmitterIRQEnabled;
        private readonly IFlagRegisterField receiverIRQEnabled;
        private readonly IFlagRegisterField txCompleteIRQEnable;
        private readonly IFlagRegisterField overrunIRQEnable;
        private readonly IFlagRegisterField noiseErrorIRQEnable;
        private readonly IFlagRegisterField framingErrorIRQEnable;
        private readonly IFlagRegisterField parityErrorIRQEnable;

        private enum Registers
        {
            BaudRateHigh = 0x00,
            BaudRateLow = 0x01,
            Control1 = 0x02,
            Control2 = 0x03,
            Status1 = 0x04,
            Status2 = 0x05,
            Control3 = 0x06,
            Data = 0x07,
        }

        private enum TransmitCompleteFlagValues
        {
            Active = 0,
            Idle = 1
        }
    }
}
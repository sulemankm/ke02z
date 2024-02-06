//
// Copyright (c) 2010-2020 Antmicro
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

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    // ICS = Internal Clock Source
    public class KE02Z_ICS : IBytePeripheral, IKnownSize
    {
        public KE02Z_ICS()
        {
            IRQ = new GPIO();

            var registersMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.Control1, new ByteRegister(this)
                    .WithValueField(6, 2, out clockSource, name: "CLKS")
                    .WithTag("RDIV", 3, 3)
                    .WithFlag(2, out internalRefSelected, name: "IREFS")
                    .WithTaggedFlag("IRCLKEN", 1)
                    .WithTaggedFlag("IREFSTN", 0)
                 },
                {(long)Registers.Control2, new ByteRegister(this)
                    .WithValueField(5, 3, out busFreqDivider, name: "BDIV")
                    .WithFlag(1, out lowPower, name: "LP")
                    //.WithReservedBits(0, 4)
                },
                {(long)Registers.Control3, new ByteRegister(this)
                    .WithValueField(0, 8, out slowInternalRefClockTrim, name: "SCTRIM")
                },
                {(long)Registers.Control4, new ByteRegister(this)
                    .WithTaggedFlag("LOLIE", 7)
                    //.WithReservedBits(6, 1)
                    .WithTaggedFlag("CME", 5)
                    //.WithReservedBits(1, 4)
                    .WithTaggedFlag("SCFTRIM", 0)
                },
                {(long)Registers.Status, new ByteRegister(this)
                    .WithFlag(7, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return true;
                    }, name: "LOLS")
                    .WithFlag(6, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return true;
                    }, name: "LOCK")
                    //.WithReservedBits(5, 1)
                    .WithFlag(4, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return internalRefSelected.Value;
                    }, name: "IREFST")
                    .WithValueField(2, 2, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return clockSource.Value;
                    }, name: "CLKST")
                    //.WithReservedBits(0, 2)
                }
            };

            registers = new ByteRegisterCollection(this, registersMap);
            // Set the reset values of registers
            registers.Write((long)Registers.Control1, (byte)0x04); // WDT enabled
            registers.Write((long)Registers.Control2, (byte)0x20); // 1kHz internal clock
            registers.Write((long)Registers.Control3, (byte)0x00); // WDT enabled
            registers.Write((long)Registers.Control4, (byte)0x00); // 1kHz internal clock
            registers.Write((long)Registers.Status, (byte)0x10); // 1kHz internal clock
        }

        public void Reset()
        {
            registers.Reset();
        }

        public byte ReadByte(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            registers.Write(offset, value);
        }

        public long Size => 0x5;
        public GPIO IRQ { get; }
        public ModesOfOperation OperationMode {
            get {
                uint clocks = (uint)registers.Read((long)Registers.Control1) & 0xC0;
                bool irefs = internalRefSelected.Value;
                bool lp = lowPower.Value;

                if (clocks == 0 && irefs == true)
                    return ModesOfOperation.FEI;
                else if (clocks == 0 && irefs == false)
                    return ModesOfOperation.FEE;
                else if (clocks == 1 && irefs == false && lp == false)
                    return ModesOfOperation.FBI;
                else if (clocks == 2 && irefs == false && lp == false)
                    return ModesOfOperation.FBE;
                else if (clocks == 2 && irefs == false && lp == true)
                    return ModesOfOperation.FBELP;
                else if (clocks == 1 && irefs == true && lp == true)
                    return ModesOfOperation.FBILP;
                else
                    return ModesOfOperation.STOP;
            }
        }
        private readonly ByteRegisterCollection registers;
        private IValueRegisterField clockSource;
        private IValueRegisterField busFreqDivider;
        private IFlagRegisterField internalRefSelected;
        private IFlagRegisterField lowPower;
        private IValueRegisterField slowInternalRefClockTrim;
        
        private enum Registers
        {
            Control1 = 0x0,
            Control2 = 0x01,
            Control3 = 0x02,
            Control4 = 0x03,
            Status = 0x04,
        }

        private enum ClockSourceValues
        {
            FLL = 0,
            Internal = 1,
            External = 2,
            Reserved = 3
        }

        public enum BusFreqDividerValues {
            DivideBy1 = 0,
            DivideBy2 = 1,
            DivideBy4 = 2,
            DivideBy8 = 3,
            DivideBy16 = 4,
            DivideBy32 = 5,
            DivideBy64 = 6,
            DivideBy128 = 7,
        }
        public enum ModesOfOperation {
            FEI, // FLL Engaged Internal
            FEE, // FLL Engaged External
            FBI, // FLL Bypassed Internal
            FBILP, // FLL Bypassed Internal Low Pass
            FBE, // FLL Bypassed External
            FBELP, // FLL Bypassed External Low Power
            STOP, // Stopped
        }
    }
}

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

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class KE02Z_SIM : IDoubleWordPeripheral, IKnownSize
    {
        public KE02Z_SIM(uint? uniqueIdHigh = null, uint? uniqueIdLow = null)
        {
            var rng = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            
            this.uniqueIdHigh = uniqueIdHigh.HasValue ? uniqueIdHigh.Value : (uint)rng.Next();
            this.uniqueIdLow = uniqueIdLow.HasValue ? uniqueIdLow.Value : (uint)rng.Next();

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ResetStatusAndId, new DoubleWordRegister(this)
                    .WithValueField(28, 4, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return 0;
                    }, name: "FAMID")
                    .WithValueField(24, 4, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return 0;
                    }, name: "SUBFAMID")
                    .WithValueField(20, 4, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return 0;
                    }, name: "RevID")
                    .WithValueField(16, 4, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return 0;
                    }, name: "PINID")
                    .WithReservedBits(14, 2)
                    .WithTaggedFlag("SACKERR", 13)
                    .WithReservedBits(12, 1)
                    .WithTaggedFlag("MDMAP", 11)
                    .WithTaggedFlag("SW", 10)
                    .WithTaggedFlag("LOCKUP", 9)
                    .WithReservedBits(8, 1)
                    .WithTaggedFlag("POR", 7)
                    .WithTaggedFlag("PIN", 6)
                    .WithTaggedFlag("WDOG", 5)
                    .WithReservedBits(3, 2)
                    .WithTaggedFlag("LOC", 2)
                    .WithTaggedFlag("LVD", 1)
                    .WithReservedBits(0, 1)
                },
                {(long)Registers.SystemOptions, new DoubleWordRegister(this)
                    .WithValueField(24, 8, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return 0;
                    }, name: "DELAY")
                    .WithTaggedFlag("DLYACT", 23)
                    .WithReservedBits(20, 3)
                    .WithTaggedFlag("CLKOE", 19)
                    .WithValueField(16, 3, valueProviderCallback: _ =>
                    {
                        return 0;
                    }, name: "BUSREF")
                    .WithTaggedFlag("TXDME", 15)
                    .WithTaggedFlag("FTMSYNC", 14)
                    .WithTaggedFlag("RXDFE", 13)
                    .WithTaggedFlag("RXDCE", 12)
                    .WithTaggedFlag("ACIC", 11)
                    .WithTaggedFlag("RTCC", 10)
                    .WithValueField(8, 2, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return 0;
                    }, name: "ADHWT")
                    .WithReservedBits(4, 4)
                    .WithTaggedFlag("SWDE", 3)
                    .WithTaggedFlag("RSTPE", 2)
                    .WithTaggedFlag("NMIE", 1)
                    .WithReservedBits(0, 1)
                },
                {(long)Registers.PinSelection, new DoubleWordRegister(this)
                    .WithReservedBits(16, 16)
                    .WithTaggedFlag("FTM2PS3", 15)
                    .WithTaggedFlag("FTM2PS2", 14)
                    .WithTaggedFlag("FTM2PS1", 13)
                    .WithTaggedFlag("FTM2PS0", 12)
                    .WithTaggedFlag("FTM1PS1", 11)
                    .WithTaggedFlag("FTM1PS0", 10)
                    .WithTaggedFlag("FTM0PS1", 9)
                    .WithTaggedFlag("FTM0PS0", 8)
                    .WithTaggedFlag("UART0PS", 7)
                    .WithTaggedFlag("SPI0PS", 6)
                    .WithTaggedFlag("I2C0PS", 5)
                    .WithTaggedFlag("RTCPS", 4)
                    .WithReservedBits(0, 4)
                },
                {(long)Registers.ClockGatingControl, new DoubleWordRegister(this)
                    .WithTaggedFlag("ACMP1", 31)
                    .WithTaggedFlag("ACMP0", 30)
                    .WithTaggedFlag("ADC", 29)
                    .WithReservedBits(28, 1)
                    .WithTaggedFlag("IRQ", 27)
                    .WithReservedBits(26, 1)
                    .WithTaggedFlag("KBI1", 25)
                    .WithTaggedFlag("KBI0", 24)
                    .WithReservedBits(23, 1)
                    .WithFlag(22, out uart2ClockGate, name: "UART2")
                    .WithFlag(21, out uart1ClockGate, name: "UART1")
                    .WithFlag(20, out uart0ClockGate, name: "UART0")
                    .WithTaggedFlag("SPI1", 19)
                    .WithTaggedFlag("SPI0", 18)
                    .WithTaggedFlag("I2C", 17)
                    .WithReservedBits(14, 3)
                    .WithTaggedFlag("SWD", 13)
                    .WithTaggedFlag("FLASH", 12)
                    .WithReservedBits(11, 1)
                    .WithTaggedFlag("CRC", 10)
                    .WithReservedBits(8, 2)
                    .WithTaggedFlag("FTM2", 7)
                    .WithTaggedFlag("FMT1", 6)
                    .WithTaggedFlag("FTM0", 5)
                    .WithReservedBits(2, 3)
                    .WithTaggedFlag("PIT", 1)
                    .WithTaggedFlag("RTC", 0)
                },
                {(long)Registers.UniqueIdLow, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return this.uniqueIdLow;
                    }, name: "SIM_UUIDL")
                },
                {(long)Registers.UniqueIdHigh, new DoubleWordRegister(this)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ =>
                    {
                        return this.uniqueIdHigh;
                    }, name: "SIM_UUIDH")
                },
                {(long)Registers.BusClockDrivider, new DoubleWordRegister(this)
                    .WithReservedBits(1, 31)
                    .WithFlag(0, out busDivider, name: "BUSDIV")
                },
            };

            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public long Size => 28;

        private readonly DoubleWordRegisterCollection registers;
        private readonly uint uniqueIdHigh;
        private readonly uint uniqueIdLow;
        private readonly IFlagRegisterField busDivider;
        private readonly IFlagRegisterField uart0ClockGate;
        private readonly IFlagRegisterField uart1ClockGate;
        private readonly IFlagRegisterField uart2ClockGate;
        private enum Registers
        {
            ResetStatusAndId = 0x0,
            SystemOptions = 0x04,
            PinSelection = 0x08,
            ClockGatingControl = 0x0C,
            UniqueIdLow = 0x10,
            UniqueIdHigh = 0x14,
            BusClockDrivider = 0x18,
        }
    }
}

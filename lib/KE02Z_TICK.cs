//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Timers
{
    public class KE02Z_TICK : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize
    {
        public KE02Z_TICK(IMachine machine)
        {
            IRQ = new GPIO();

            //machine.ClockSource.AddClockEntry(new ClockEntry(periodInMs, 1000, OnTick, this, String.Empty));

            timer = new LimitTimer(machine.ClockSource, InitialFrequency, this, "sysTick", enabled: false, eventEnabled: true);
            timer.LimitReached += () =>
            {
                this.Log(LogLevel.Noisy, "Limit reached");
                if(tickInterrupt.Value)
                {
                    IRQ.Blink();
                }

                this.Log(LogLevel.Info, "SysTick timed out. Resetting...");
                machine.RequestReset();
            };

            var registersMap = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.ControlAndStatus, new DoubleWordRegister(this) //Define(this, resetValue: 0x80)
                    .WithReservedBits(17, 15)
                    .WithFlag(16, out countFlag, name: "COUNTFLAG")
                    .WithReservedBits(3, 13)
                    .WithFlag(2, out clockSource, name: "CLKSOURCE")
                    .WithFlag(1, out tickInterrupt, name: "TICKINT")
                    .WithFlag(0, out enabled, name: "ENABLE")
                },
                {(long)Registers.ReloadValue, new DoubleWordRegister(this) //Define(this, resetValue: 0x80)
                    .WithReservedBits(24, 8)
                    .WithValueField(0, 24, writeCallback: (_, value) => {
                        reloadValue.Value = value;
                    }, name: "RELOAD")
                },
                {(long)Registers.CurrentValue, new DoubleWordRegister(this) //Define(this, resetValue: 0x80)
                    .WithReservedBits(24, 8)
                    .WithValueField(0, 24, writeCallback: (_, value) => {
                        currentValue.Value = value;
                    }, name: "CURRENT")
                },
                {(long)Registers.Calibration, new DoubleWordRegister(this) //Define(this, resetValue: 0x80)
                    .WithTaggedFlag("NOREF", 31)
                    .WithTaggedFlag("COUNTFLAG", 30)
                    .WithReservedBits(24, 6)
                    .WithValueField(0, 24, writeCallback: (_, value) => {
                        tenMs.Value = value;
                    }, name: "TENMS")
                },
            };
            registers = new DoubleWordRegisterCollection(this, registersMap);
        }

        public long Size => 0x10;

        public GPIO IRQ { get; }

        public bool Enabled {
            get {
                return enabled.Value;
            }
        }
        public uint RVR {
            get{
                return (uint)reloadValue.Value;
            }
        }
        public uint CVR {
            get{
                return (uint)currentValue.Value;
            }
        }

        public void Reset()
        {
            registers.Reset();
            //timer.Reset();
        }

        public virtual uint ReadDoubleWord(long offset)
        {
            return (uint)registers.Read(offset);
        }

        public virtual void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public ushort ReadWord(long offset) {
            byte b1 = (byte)registers.Read(offset);
            byte b2 = (byte)registers.Read(offset+1);
            return BitConverter.ToUInt16(new byte[2] {b2 , b1}, 0);
        }
        public void WriteWord(long offset, ushort value) {
            byte b1 = (byte)(value & 0x00FF);
            byte b2 = (byte)((value & 0xFF00) >> 8);
            registers.Write(offset, b1);
            registers.Write(offset+1, b2);
        }
        public byte ReadByte(long offset)
        {
            byte b = (byte)registers.Read(offset);
            //Console.Write("UART Read: " + b);
            return b;
        }

        public void WriteByte(long offset, byte value)
        {
            registers.Write(offset, value);
            //Console.Write("UART Write: " + value);
        }

        private readonly DoubleWordRegisterCollection registers;
        private readonly LimitTimer timer;
        private const int InitialFrequency = 1000; // 1/1ms = 1000 Hz

        private IFlagRegisterField countFlag;
        private IFlagRegisterField clockSource;
        private IFlagRegisterField tickInterrupt;
        private IFlagRegisterField enabled;

        private IValueRegisterField reloadValue;
        private IValueRegisterField currentValue;
        private IValueRegisterField tenMs;

        private enum Registers
        {
            ControlAndStatus = 0x0,  // SYST_CSR
            ReloadValue = 0x4,  // SYST_RVR
            CurrentValue = 0x8,  // SYST_CVR
            Calibration = 0xC,  // SYST_CALIB
        };

    }
};

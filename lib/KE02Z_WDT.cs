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
    public class KE02Z_WDT : IWordPeripheral, IBytePeripheral, IKnownSize
    {
        public KE02Z_WDT(IMachine machine)
        {
            IRQ = new GPIO();

            watchdogTimer = new LimitTimer(machine.ClockSource, InitialFrequency, this, "watchdog", enabled: false, eventEnabled: true);
            watchdogTimer.LimitReached += () =>
            {
                this.Log(LogLevel.Noisy, "Limit reached");
                if(interruptRequestEnable.Value)
                {
                    IRQ.Blink();
                }

                this.Log(LogLevel.Info, "Watchdog timed out. Resetting...");
                machine.RequestReset();
            };

            var registersMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.ControlAndStatus1, new ByteRegister(this) //Define(this, resetValue: 0x80)
                    .WithFlag(7, writeCallback: (oldVal, newVal) => 
                    {
                        watchdogTimer.Enabled = newVal;
                    }, name: "EN")
                    .WithFlag(6, out interruptRequestEnable, writeCallback: (oldVal, newVal) =>
                    {
                        interruptRequestEnable.Value = newVal;
                    }, name: "INT")
                    .WithFlag(5, out update, name: "UPDATE")
                    .WithValueField(3, 2, writeCallback: (_, value) => {
                        watchdogTest = (WatchdogTest)value;
                    }, name: "TST")
                    .WithTaggedFlag("DBG", 2)
                    .WithTaggedFlag("WAIT", 1)
                    .WithTaggedFlag("STOP", 0)
                },
                {(long)Registers.ControlAndStatus2, new ByteRegister(this) //Define(this, resetValue: 0x01)
                    .WithFlag(7, out windowMode, name: "WIN")
                    .WithFlag(6, out watchdogInterruptFlag, writeCallback: (oldVal, newVal) =>
                    {
                        watchdogInterruptFlag.Value = newVal;
                    }, name: "FLG")
                    .WithTaggedFlag("RESERVED", 5)
                    .WithFlag(4, out watchdogPrescalerEnabled, writeCallback: (oldVal, newVal) =>
                    {
                        watchdogPrescalerEnabled.Value = newVal;
                    }, name: "PRES")
                    .WithValueField(2, 2, name: "ZEROS")
                    .WithValueField(0, 2, writeCallback: (KE02Z_WDT, value) => {
                        // out watchdogClock, FieldMode.Write
                        watchdogClock = (WatchdogClock)value;
                    }, name: "CLK")
                },
                {(long)Registers.CounterHi, new ByteRegister(this) //.Define(this, resetValue: 0x00)
                    .WithValueField(0, 8, out counterHigh, writeCallback: (oldVal, newVal) =>
                    {
                        counterHigh.Value = newVal;
                    }, name: "CNTHI")
                },
                {(long)Registers.CounterLo, new ByteRegister(this) //.Define(this, resetValue: 0x00)
                    .WithValueField(0, 8, out counterLow, writeCallback: (oldVal, newVal) =>
                    {
                        counterLow.Value = newVal;
                    }, name: "CNTLO")
                },
                {(long)Registers.TimeOutValHi, new ByteRegister(this) //.Define(this, resetValue: 0x00)
                    .WithValueField(0, 8, out timeOutValueHigh, writeCallback: (oldVal, newVal) =>
                    {
                        timeOutValueHigh.Value = newVal;
                    }, name: "TOVALHIGH")
                },
                {(long)Registers.TimeOutValLo, new ByteRegister(this) //.Define(this, resetValue: 0x00)
                    .WithValueField(0, 8, out timeOutValueLow, writeCallback: (oldVal, newVal) =>
                    {
                        timeOutValueLow.Value = newVal;
                    }, name: "TOVALLOW")
                },
                {(long)Registers.WindowHi, new ByteRegister(this) //.Define(this, resetValue: 0x00)
                    .WithValueField(0, 8, out windowValueHigh, writeCallback: (oldVal, newVal) =>
                    {
                        windowValueHigh.Value = newVal;
                    }, name: "WINHIGH")
                },
                {(long)Registers.WindowLo, new ByteRegister(this) //.Define(this, resetValue: 0x00)
                    .WithValueField(0, 8, out windowValueLow, writeCallback: (oldVal, newVal) =>
                    {
                        windowValueLow.Value = newVal;
                    }, name: "WINLOW")
                }
            };
            registers = new ByteRegisterCollection(this, registersMap);

            // Set default values of registers
            registers.Write((long)Registers.ControlAndStatus1, (byte)0x80); // WDT enabled
            registers.Write((long)Registers.ControlAndStatus2, (byte)0x01); // 1kHz internal clock
        }

        public long Size => 0x8;

        public GPIO IRQ { get; }
        public bool Enabled {
            get {
                return watchdogTimer.Enabled;
            }
        }

        public byte Clock {
            get{
                uint b = registers.Read((long)Registers.ControlAndStatus2);
                return (byte)(b & 0x03);
            }
        }
        public void Reset()
        {
            registers.Reset();
            watchdogTimer.Reset();
        }

        public ushort ReadWord(long offset) {
            byte b1 = registers.Read(offset);
            byte b2 = registers.Read(offset+1);
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
            byte b = registers.Read(offset);
            //Console.Write("UART Read: " + b);
            return b;
        }

        public void WriteByte(long offset, byte value)
        {
            registers.Write(offset, value);
            //Console.Write("UART Write: " + value);
        }

        private readonly ByteRegisterCollection registers;
        private bool WatchdogZero => watchdogTimer.Value == watchdogTimer.Limit;
        private readonly LimitTimer watchdogTimer; // OK
        private const int InitialFrequency = 250; // 1/4ms = 250 Hz

        private WatchdogTest watchdogTest;
        private WatchdogClock watchdogClock;

        private IFlagRegisterField update;
        private IFlagRegisterField interruptRequestEnable;
        private IFlagRegisterField windowMode;
        private IFlagRegisterField watchdogInterruptFlag;
        private IFlagRegisterField watchdogPrescalerEnabled; // 256 prescaler enable/disable

        private IValueRegisterField counterHigh;
        private IValueRegisterField counterLow;
        private IValueRegisterField timeOutValueHigh;
        private IValueRegisterField timeOutValueLow;
        private IValueRegisterField windowValueHigh;
        private IValueRegisterField windowValueLow;

        private const ushort RefreshKeyHi = 0x02A6;
        private const ushort RefreshKeyLo = 0x80B4;
        private const ushort UnlockKeyHi = 0x20C5;
        private const ushort UnlockKeyLo = 0x28D9;

        private enum Registers
        {
            ControlAndStatus1 = 0x0,  // WDOG_CS1
            ControlAndStatus2 = 0x1,  // WDOG_CS2
            CounterHi = 0x2,  // WDOG_CNTH
            CounterLo = 0x3,  // WDOG_CNTL
            TimeOutValHi = 0x4,  // WDOG_TOVALH
            TimeOutValLo = 0x5,  // WDOG_TOVALL
            WindowHi = 0x6, // WDOG_WINH
            WindowLo = 0x7, // WDOG_WINL
        };

        private enum WatchdogClock
        {
            BusClock = 0b00,
            LpoClock1kHz = 0b01, // Low Power Oscillator 1kHz clock
            IcsIrClock32kHz = 0b10, // Internal 32kHz clock
            ExternalClock = 0b11,
        };

        private enum WatchdogTest
        {
            TestModeDisabled = 0b00,
            UserModeEnabled = 0b01,
            TestModeEnabledLowByte = 0b10,
            TestModeEnabledHighByte = 0b11,
        };
    }
};

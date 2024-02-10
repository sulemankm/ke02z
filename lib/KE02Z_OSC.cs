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
using IronPython.Modules;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class KE02Z_OSC : IBytePeripheral, IKnownSize
    {
        public KE02Z_OSC(IMachine machine)
        {
            var registersMap = new Dictionary<long, ByteRegister>
            {
                {(long)Registers.Control, new ByteRegister(this)
                    .WithFlag(7, out oscEnable, name: "OSCEN")
                    .WithReservedBits(6, 1)
                    .WithFlag(5, out oscEnableInStopMode, name: "OSCSTEN")
                    .WithFlag(4, out oscOutputSelect, writeCallback: (_, val) =>{
                        oscOutputSelect.Value = val;
                        oscInit.Value = val; // Simulate Osc as initialized if OscOutputSelect is true
                    }, name: "OSCOS")
                    .WithReservedBits(3, 1)
                    .WithFlag(2, out oscFreqRangeSelect, name: "RANGE")
                    .WithFlag(1, out highGainOscSelect, name: "HGO")
                    .WithFlag(0, out oscInit, name: "OSCINIT")
                },
            };

            registers = new ByteRegisterCollection(this, registersMap);
        }

        public byte ReadByte(long offset)
        {
            return (byte)registers.Read(offset);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public void WriteByte(long offset, byte value)
        {
            registers.Write(offset, value);
        }

        public long Size => 1;

        private readonly ByteRegisterCollection registers;
        private IFlagRegisterField oscEnable;
        private IFlagRegisterField oscEnableInStopMode;
        private IFlagRegisterField oscOutputSelect;
        private IFlagRegisterField oscFreqRangeSelect;
        private IFlagRegisterField highGainOscSelect;
        private IFlagRegisterField oscInit;
        private enum Registers
        {
            Control = 0x0,
        }
    }
}

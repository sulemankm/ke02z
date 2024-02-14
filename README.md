# ke02z
Emulation for NXP Kinetis KE02Z microcontroller using [Renode](https://github.com/sulemankm/renode).

## Command Script Commands
Get and Set VectorTable Offset:
    sysbus.cpu VectorTableOffset // get
    sysbus.cpu VectorTableOffset 0xYourValue //set
    
Enable logging of function names:

    cpu LogFunctionNames true true

Enable logging of all Peripherals access:
    sysbus LogAllPeripheralsAccess true

Enable logging of specific Peripheral access:
    sysbus LogPeripheralAccess uart1

Loading a peripheral using script in a string:
    machine LoadPlatformDescriptionFromString 'uart1: UART.KE02Z_UART @ sysbus 0x4006B000 { IRQ -> nvic@13 }'

Create Server Socket:
    emulation CreateServerSocketTerminal 3456 "term" // OR
    emulation CreateServerSocketTerminal 3456 "term" false // to suppress initial config bytes
    connector Connect sysbus.uart0 term

Assign a binary executable file to a variable:
    $bin?=@F:\workspaces\mcuXpresso-ws\MKE02Z4_Blinky\Debug\MKE02Z4_Blinky.axf      // or
    $bin?=@C:\Users\sulem\mcuXpresso-ws\MKE02Z4_Blinky\Release\MKE02Z4_Blinky.axf   // or
    $bin?=@F:\workspaces\mcuXpresso-ws\ecu\mcux\microjet_ecu\Debug\microjet_ecu.axf // etc

Load binary(.bin or .hex) file:
    sysbus LoadBin

Load Elf file:

    sysbus LoadELF $bin

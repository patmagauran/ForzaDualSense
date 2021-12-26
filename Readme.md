This is a program to control lights and adaptive triggers on a DualSense 5 controller using DualSenseX for compatible Forza Games.

## Setup:

In Forza, under HUD turn on the UDP data out, with an IP of 127.0.0.1 and a port of 5300.
In DualSenseX under the controller settings, set the UDP port to 6750.
It should work.

## Running

This is compiled with .net core 6.0. Download the SDK from microsoft and run using dotnet run --project ForzaDualSense

## Thanks and Credits

[DualSenseX](https://github.com/Paliverse/DualSenseX)
[Forza-Telemetry](https://github.com/austinbaccus/forza-telemetry/tree/main/ForzaCore)

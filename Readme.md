## Archival

As of November 23, 2023. The Repo maintained by cosmii02: https://github.com/cosmii02/ForzaDSX provides a more stable experience, a UI to customize the settings the originally differed between the two repos, and a larger user base. For those reasons, I am choosing to archive this and direct everyone to that repo.



## Intro

This is a program to control lights and adaptive triggers on a DualSense 5 controller using DualSenseX for compatible Forza Games.

**NOTE:** This is currently in the alpha stage. It works well and provides decent feedback via the adaptive triggers. However it is not perfect and definetly has room for improvement. I welcome people to help make needed adjustments(See below for more information).

**WARNING:** Since we do not have exact specifications of the commands sent to the controller, it is possible that certain values may be out of specification or in the not-reccomended categories. This could cause damage to controllers. Although I use this program without issue, I will not be held liable for damage to your controller due to it's use.

## Adjusting / Modifying

This program is written in very simple C#. If you do not know C#, it should not be hard to learn the necessary parts. If you do not know any programming, it may be more difficult, but I have left the relevant code very well documented.

The only file you should need to modify is `Program.cs` Within it the conversion from Forza Data to dual Sense output is all handled in the function `SendData` which should be the top most function. I reccomend understanding this fully and taking the rest of the code for granted as the other code shouldn't need to be modified. If you do care to modify it, please try to understand it based on context and the comments. See the credits for the sources.

The `SendData` function takes the parsed data from Forza and decides how best to adapt the triggers. It does this by setting the necesary parameters on the message to send to DualSenseX and sending it. See the `Test` function for examples. The parameters are documented further in the code.

There are not any commands that can be sent to DualSenseX that cannot be accessed via the DualSenseX UI. That makes that UI a great way to test the feeling of different parameters. Based on warnings I have seen, avoid setting frequency to anything above 40 Hz.

If you feel your modifications are better, please sumbit them via a PR request. I will also be available on the DualSenseX discord server to discuss how to replicate real brakes/throttle.

**Note:** I do not know exactly how to best replicate a good brake feel. If anyone has access to F1 2021 on PC or any of the adaptive control racing games on PS5 and would like to contribute with descriptions of how the triggers react in different situationsm, that would be great.

## Setup:

In Forza, under HUD turn on the UDP data out, with an IP of 127.0.0.1 and a port of 5300.
![image](https://user-images.githubusercontent.com/10986886/147594037-02f9f215-d59f-4e09-9fb4-131cacff2615.png)

In DualSenseX under the controller settings, set the UDP port to your preference and ensure the UDP Port Listener is enabled. The program should automatically use what is set.
<img width="1250" alt="image" src="https://user-images.githubusercontent.com/10986886/147705786-8d2930fe-6050-48a0-8491-e8318486f426.png">

It should work.

## Running Release

Download the Release from the releases page. Extract the Zip folder to a preferred location. Run the Executable to enjoy adaptive triggers on Forza!

## appsettings.ini

There is a settings file in the release. You can change the settings to your preference.

## Running From Source

1. Install the .Net Core 6.0 SDK (x64 version) from here: [https://dotnet.microsoft.com/en-us/download/dotnet/6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
2. Clone this repository with git
3. While in the top level of this repository, run `dotnet run --project ForzaDualSense`.
   - Top level here refers to the main directory of the repository such that the subdirectories are `.vscode` and `ForzaDualSense`

## Thanks and Credits

[DualSenseX](https://github.com/Paliverse/DualSenseX)

[Forza-Telemetry](https://github.com/austinbaccus/forza-telemetry/tree/main/ForzaCore)

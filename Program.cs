using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using System.IO;
using CsvHelper;
using System.Globalization;

namespace ForzaDSX
{
    class Program
    {
        public const String VERSION = "0.4.1";
        static Settings settings = new Settings();
        static bool verbose = false;
        static bool logToCsv = false;
        static String csvFileName = "";
        public const int CSV_BUFFER_LENGTH = 120;
        static int lastThrottleResistance = 1;
		static int lastThrottleFreq = 0;
		static int lastBrakeResistance = 200;
        static int lastBrakeFreq = 0;

        static uint LastValidCarClass = 0;
		static int LastValidCarCPI = 0;
        static float MaxCPI = 255;

        static float LastEngineRPM = 0;
		// FH does not always correctly set IsRaceOn, so we must also check if the RPM info is the same for a certain ammount of time
		static uint LastRPMAccumulator = 0;
        static uint RPMAccumulatorTriggerRaceOff = 200;

		// Colors for Light Bar while in menus -> using car's PI colors from Forza
		public static readonly uint CarClassD = 0;
        public static readonly int[] ColorClassD = { 107, 185, 236 };
        public static readonly uint CarClassC = 1;
        public static readonly int[] ColorClassC = { 234, 202, 49 };
        public static readonly uint CarClassB = 2;
        public static readonly int[] ColorClassB = { 211, 90, 37 };
        public static readonly uint CarClassA = 3;
        public static readonly int[] ColorClassA = { 187, 59, 34 };
        public static readonly uint CarClassS1 = 4;
        public static readonly int[] ColorClassS1 = { 128, 54, 243 };
        public static readonly uint CarClassS2 = 5;
		public static readonly int[] ColorClassS2 = { 75, 88, 229 };

		public static readonly int[] ColorClassX = { 105, 182, 72 };
        

		//This sends the data to DSX based on the input parsed data from Forza.
		//See DataPacket.cs for more details about what forza parameters can be accessed.
		//See the Enums at the bottom of this file for details about commands that can be sent to DualSenseX
		//Also see the Test Function below to see examples about those commands
		static void SendData(DataPacket data, CsvWriter csv)
        {
            Packet p = new Packet();
            CsvData csvRecord = new CsvData();
            //Set the controller to do this for
            int controllerIndex = 0;
            
            //Initialize our array of instructions
            p.instructions = new Instruction[4];
            if (logToCsv)
            {
                csvRecord.time = data.TimestampMS;
                csvRecord.AccelerationX = data.AccelerationX;
                csvRecord.AccelerationY = data.AccelerationY;
                csvRecord.AccelerationZ = data.AccelerationZ;
                csvRecord.Brake = data.Brake;
                csvRecord.TireCombinedSlipFrontLeft = data.TireCombinedSlipFrontLeft;
                csvRecord.TireCombinedSlipFrontRight = data.TireCombinedSlipFrontRight;
                csvRecord.TireCombinedSlipRearLeft = data.TireCombinedSlipRearLeft;
                csvRecord.TireCombinedSlipRearRight = data.TireCombinedSlipRearRight;
                csvRecord.CurrentEngineRpm = data.CurrentEngineRpm;
            }

            /* Combined variables */
            int resistance = 0;
            int filteredResistance = 0;
            float avgAccel = 0;
            int freq = 0;
            int filteredFreq = 0;

            bool bInRace = data.IsRaceOn;

			float currentRPM = data.CurrentEngineRpm;

			// FH does not always correctly set IsRaceOn, so we must also check if the RPM info is the same for a certain ammount of time
            // Also check if Power <= 0 (car is really stopped)
			if (currentRPM == LastEngineRPM
				&& data.Power <= 0)
            {
                LastRPMAccumulator++;
                if(LastRPMAccumulator > RPMAccumulatorTriggerRaceOff)
                {
                    bInRace = false;
                }
            }
            else
            {
                LastRPMAccumulator = 0;
			}

			LastEngineRPM = currentRPM;

			//Get average tire slippage. This value runs from 0.0 upwards with a value of 1.0 or greater meaning total loss of grip.
			float combinedTireSlip = (Math.Abs(data.TireCombinedSlipFrontLeft) + Math.Abs(data.TireCombinedSlipFrontRight) + Math.Abs(data.TireCombinedSlipRearLeft) + Math.Abs(data.TireCombinedSlipRearRight)) / 4;
			float combinedFrontTireSlip = (Math.Abs(data.TireCombinedSlipFrontLeft) + Math.Abs(data.TireCombinedSlipFrontRight)) / 2;
			float combinedRearTireSlip = (Math.Abs(data.TireCombinedSlipRearLeft) + Math.Abs(data.TireCombinedSlipRearRight)) / 2;

			uint currentClass = LastValidCarClass;
            if (data.CarClass > 0)
            {
				LastValidCarClass = currentClass = data.CarClass;
			}

            int currentCPI = LastValidCarCPI;
			if (data.CarPerformanceIndex > 0)
			{
				LastValidCarCPI = currentCPI = Math.Min((int)data.CarPerformanceIndex, 255);
			}

			// Right Trigger (index 2)
			p.instructions[2].type = InstructionType.TriggerUpdate;

            // Left Trigger
			p.instructions[0].type = InstructionType.TriggerUpdate;

            // Light Bar
			p.instructions[1].type = InstructionType.RGBUpdate;

			// No race = normal triggers
			if (!bInRace)
            {
				p.instructions[2].parameters = new object[] { controllerIndex, Trigger.Right, TriggerMode.Normal, 0, 0 };
				p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Normal, 0, 0 };

				#region Light Bar color
				int CPIcolorR = 255;
				int CPIcolorG = 255;
				int CPIcolorB = 255;

                float cpiRatio = currentCPI / MaxCPI;

				if (currentClass <= CarClassD)
                {
					CPIcolorR = (int)Math.Floor(cpiRatio * ColorClassD[0]);
					CPIcolorG = (int)Math.Floor(cpiRatio * ColorClassD[1]);
					CPIcolorB = (int)Math.Floor(cpiRatio * ColorClassD[2]);
				}
                else if (currentClass <= CarClassC)
                {
                    CPIcolorR = (int)Math.Floor(cpiRatio * ColorClassC[0]);
                    CPIcolorG = (int)Math.Floor(cpiRatio * ColorClassC[1]);
                    CPIcolorB = (int)Math.Floor(cpiRatio * ColorClassC[2]);
                }
                else if (currentClass <= CarClassB)
                {
                    CPIcolorR = (int)Math.Floor(cpiRatio * ColorClassB[0]);
                    CPIcolorG = (int)Math.Floor(cpiRatio * ColorClassB[1]);
                    CPIcolorB = (int)Math.Floor(cpiRatio * ColorClassB[2]);
                }
                else if (currentClass <= CarClassA)
                {
                    CPIcolorR = (int)Math.Floor(cpiRatio * ColorClassA[0]);
                    CPIcolorG = (int)Math.Floor(cpiRatio * ColorClassA[1]);
                    CPIcolorB = (int)Math.Floor(cpiRatio * ColorClassA[2]);
                }
                else if (currentClass <= CarClassS1)
                {
                    CPIcolorR = (int)Math.Floor(cpiRatio * ColorClassS1[0]);
                    CPIcolorG = (int)Math.Floor(cpiRatio * ColorClassS1[1]);
                    CPIcolorB = (int)Math.Floor(cpiRatio * ColorClassS1[2]);
                }
                else if (currentClass <= CarClassS2)
                {
                    CPIcolorR = (int)Math.Floor(cpiRatio * ColorClassS2[0]);
                    CPIcolorG = (int)Math.Floor(cpiRatio * ColorClassS2[1]);
                    CPIcolorB = (int)Math.Floor(cpiRatio * ColorClassS2[2]);
                }
                else
				{
					CPIcolorR = ColorClassX[0];
					CPIcolorG = ColorClassX[1];
					CPIcolorB = ColorClassX[2];
				}

				p.instructions[1].parameters = new object[] { controllerIndex, CPIcolorR, CPIcolorG, CPIcolorB };
				#endregion

				if (logToCsv)
                {
                    csvRecord.AverageAcceleration = 0;
                    csvRecord.ThrottleResistance = 0;
                    csvRecord.ThrottleResistance_filtered = 0;

					csvRecord.BrakeResistance = resistance;
					csvRecord.combinedTireSlip = combinedTireSlip;
					csvRecord.BrakeVibrationFrequency = freq;
					csvRecord.BrakeResistance_filtered = filteredResistance;
					csvRecord.BrakeVibrationFrequency_freq = filteredFreq;
				}

                if (verbose)
                {
                    Console.WriteLine($"No race going on. Normal Triggers. Car's Class = {currentClass}; CPI = {currentCPI}; CPI Ratio = {cpiRatio}; Color [{CPIcolorR}, {CPIcolorG}, {CPIcolorB}]");
                }
            }
            else
            {
				#region Right Trigger
				//Set the updates for the right Trigger(Throttle)

				avgAccel = (float)Math.Sqrt((settings.TURN_ACCEL_MOD * (data.AccelerationX * data.AccelerationX)) + (settings.FORWARD_ACCEL_MOD * (data.AccelerationZ * data.AccelerationZ)));

                // Define losing grip as front tires slipping or rear tires slipping while accelerating a fair ammount
                bool bLosingAccelGrip =
                    combinedFrontTireSlip > settings.THROTTLE_GRIP_LOSS_VAL
                    || (combinedRearTireSlip > settings.THROTTLE_GRIP_LOSS_VAL && data.Accelerator > 200);

				// If losing grip, start to "vibrate"
				if (bLosingAccelGrip)
                {
                    freq = (int)Math.Floor(Map(combinedTireSlip, settings.THROTTLE_GRIP_LOSS_VAL, 5, 0, settings.MAX_ACCEL_GRIPLOSS_VIBRATION));
					resistance = (int)Math.Floor(Map(avgAccel, 0, settings.ACCELRATION_LIMIT, settings.MIN_ACCEL_GRIPLOSS_STIFFNESS, settings.MAX_ACCEL_GRIPLOSS_STIFFNESS));
					//resistance = settings.MIN_ACCEL_GRIPLOSS_STIFFNESS - (int)Math.Floor(Map(data.Accelerator, 0, 255, settings.MIN_ACCEL_GRIPLOSS_STIFFNESS, settings.MAX_ACCEL_GRIPLOSS_STIFFNESS));
					filteredResistance = (int)Math.Floor(EWMA(resistance, lastThrottleResistance, settings.EWMA_ALPHA_THROTTLE) * settings.RIGHT_TRIGGER_EFFECT_INTENSITY);
                    filteredFreq = (int)Math.Floor(EWMA(freq, lastThrottleFreq, settings.EWMA_ALPHA_THROTTLE_FREQ) * settings.RIGHT_TRIGGER_EFFECT_INTENSITY);
                    
                    lastThrottleResistance = filteredResistance;
                    lastThrottleFreq = filteredFreq;

					if (filteredFreq <= settings.MIN_ACCEL_GRIPLOSS_VIBRATION
                        || data.Accelerator <= settings.THROTTLE_VIBRATION_MODE_START)
                    {
                        p.instructions[2].parameters = new object[] { controllerIndex, Trigger.Right, TriggerMode.Resistance, 0, filteredResistance };

                        filteredFreq = 0;
                        filteredResistance = 0;
					}
                    else
                    {
                        p.instructions[2].parameters = new object[] { controllerIndex, Trigger.Right, TriggerMode.CustomTriggerValue, CustomTriggerValueMode.VibrateResistance, filteredFreq, filteredResistance, settings.BRAKE_VIBRATION_START, 0, 0, 0, 0 };
                    }
					if (verbose)
					{
						Console.WriteLine($"Setting Throttle to vibration mode with freq: {filteredFreq}, Resistance: {filteredResistance}");
					}
				}
                else
                {
                    //It should probably always be uniformly stiff
                    resistance = (int)Math.Floor(Map(avgAccel, 0, settings.ACCELRATION_LIMIT, settings.MIN_THROTTLE_RESISTANCE, settings.MAX_THROTTLE_RESISTANCE));
                    filteredResistance = (int)Math.Floor(EWMA(resistance, lastThrottleResistance, settings.EWMA_ALPHA_THROTTLE) * settings.RIGHT_TRIGGER_EFFECT_INTENSITY);
                    
                    lastThrottleResistance = filteredResistance;
                    p.instructions[2].parameters = new object[] { controllerIndex, Trigger.Right, TriggerMode.Resistance, 0, filteredResistance };
                }

				if (logToCsv)
				{
					csvRecord.AverageAcceleration = avgAccel;
					csvRecord.ThrottleResistance = resistance;
					csvRecord.ThrottleResistance_filtered = filteredResistance;
				}

				if (verbose)
                {
                    Console.WriteLine($"Average Acceleration: {avgAccel}; Throttle Resistance: {filteredResistance}; Accelerator: {data.Accelerator}");
                }
                #endregion

				#region Left Trigger
				//Update the left(Brake) trigger

				if (combinedTireSlip < settings.GRIP_LOSS_VAL && data.Brake < settings.BRAKE_VIBRATION__MODE_START)
                {
                    freq = settings.MAX_BRAKE_VIBRATION - (int)Math.Floor(Map(combinedTireSlip, settings.GRIP_LOSS_VAL, 1, 0, settings.MAX_BRAKE_VIBRATION));
                    resistance = settings.MIN_BRAKE_STIFFNESS - (int)Math.Floor(Map(data.Brake, 0, 255, settings.MAX_BRAKE_STIFFNESS, settings.MIN_BRAKE_STIFFNESS));
                    filteredResistance = (int)Math.Floor(EWMA(resistance, lastBrakeResistance, settings.EWMA_ALPHA_BRAKE) * settings.LEFT_TRIGGER_EFFECT_INTENSITY);
                    filteredFreq = (int)Math.Floor(EWMA(freq, lastBrakeFreq, settings.EWMA_ALPHA_BRAKE_FREQ) * settings.LEFT_TRIGGER_EFFECT_INTENSITY);
                    lastBrakeFreq = filteredFreq;
                    lastBrakeResistance = filteredResistance;
                    if (filteredFreq <= settings.MIN_BRAKE_VIBRATION)
                    {
                        p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Resistance, 0, 0 };

                    }
                    else
                    {
                        p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.CustomTriggerValue, CustomTriggerValueMode.VibrateResistance, filteredFreq, filteredResistance, settings.BRAKE_VIBRATION_START, 0, 0, 0, 0 };

                    }
                    //Set left trigger to the custom mode VibrateResitance with values of Frequency = freq, Stiffness = 104, startPostion = 76. 
                    if (verbose)
                    {
                        Console.WriteLine($"Setting Brake to vibration mode with freq: {filteredFreq}, Resistance: {filteredResistance}");
                    }

                }
                else
                {
                    //By default, Increasingly resistant to force
                    resistance = (int)Math.Floor(Map(data.Brake, 0, 255, settings.MIN_BRAKE_RESISTANCE, settings.MAX_BRAKE_RESISTANCE));
                    filteredResistance = (int)Math.Floor(EWMA(resistance, lastBrakeResistance, settings.EWMA_ALPHA_BRAKE) * settings.LEFT_TRIGGER_EFFECT_INTENSITY);
                    lastBrakeResistance = filteredResistance;
                    p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Resistance, 0, filteredResistance };
                }
                if (verbose)
                {
                    Console.WriteLine($"Brake: {data.Brake}; Brake Resistance: {filteredResistance}; Tire Slip: {combinedTireSlip}");
                }
                if (logToCsv)
                {
                    csvRecord.BrakeResistance = resistance;
                    csvRecord.combinedTireSlip = combinedTireSlip;
                    csvRecord.BrakeVibrationFrequency = freq;
                    csvRecord.BrakeResistance_filtered = filteredResistance;
                    csvRecord.BrakeVibrationFrequency_freq = filteredFreq;

                }
                #endregion

                #region Light Bar
                //Update the light bar
                //Currently registers intensity on the green channel based on engine RPM as a percantage of the maxium. Changes to red if RPM ratio > 80% (usually red line)
                float engineRange = data.EngineMaxRpm - data.EngineIdleRpm;
				float CurrentRPMRatio = (currentRPM - data.EngineIdleRpm) / engineRange;
                int GreenChannel = Math.Max((int)Math.Floor(CurrentRPMRatio * 255), 50);
                int RedChannel = (int)Math.Floor(CurrentRPMRatio * 255);
				if (CurrentRPMRatio >= settings.RPM_REDLINE_RATIO)
                {
                    // Remove Green
					GreenChannel = 255 - GreenChannel;
				}

				p.instructions[1].parameters = new object[] { controllerIndex, RedChannel, GreenChannel, 0 };

				if (verbose)
                {
                    Console.WriteLine($"Engine RPM: {data.CurrentEngineRpm}; Engine Max RPM: {data.EngineMaxRpm}; Engine Idle RPM: {data.EngineIdleRpm}");
                }
				#endregion
			}

			if (logToCsv)
            {
                csv.WriteRecord(csvRecord);
                csv.NextRecord();
            }
            //Send the commands to DualSenseX
            Send(p);


        }
        //This is the same test method from the UDPExample in DualSenseX. It just provides a basic overview of the different commands that can be used with DualSenseX.
        static void test(string[] args)
        {


            while (true)
            {
                Packet p = new Packet();

                int controllerIndex = 0;

                p.instructions = new Instruction[4];

                // ----------------------------------------------------------------------------------------------------------------------------

                //Normal:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Normal };

                //GameCube:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.GameCube };

                //VerySoft:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.VerySoft };

                //Soft:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Soft };

                //Hard:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Hard };

                //VeryHard:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.VeryHard };

                //Hardest:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Hardest };

                //Rigid:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Rigid };

                //VibrateTrigger needs 1 param of value from 0-255:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.VibrateTrigger, 10 };

                //Choppy:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Choppy };

                //Medium:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Medium };

                //VibrateTriggerPulse:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.VibrateTriggerPulse };

                //CustomTriggerValue with CustomTriggerValueMode:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.CustomTriggerValue, CustomTriggerValueMode.PulseAB, 0, 101, 255, 255, 0, 0, 0 };

                //Resistance needs 2 params Start: 0-9 Force:0-8:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Resistance, 0, 8 };

                //Bow needs 4 params Start: 0-8 End:0-8 Force:0-8 SnapForce:0-8:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Bow, 0, 8, 2, 5 };

                //Galloping needs 5 params Start: 0-8 End:0-9 FirstFoot:0-6 SecondFoot:0-7 Frequency:0-255:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Galloping, 0, 9, 2, 4, 10 };

                //SemiAutomaticGun needs 3 params Start: 2-7 End:0-8 Force:0-8:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.SemiAutomaticGun, 2, 7, 8 };

                //AutomaticGun needs 3 params Start: 0-8 End:0-9 StrengthA:0-7 StrengthB:0-7 Frequency:0-255 Period 0-2:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.AutomaticGun, 0, 8, 10 };

                //AutomaticGun needs 6 params Start: 0-9 Strength:0-8 Frequency:0-255:
                p.instructions[0].type = InstructionType.TriggerUpdate;
                p.instructions[0].parameters = new object[] { controllerIndex, Trigger.Left, TriggerMode.Machine, 0, 9, 7, 7, 10, 0 };

                // ----------------------------------------------------------------------------------------------------------------------------

                p.instructions[1].type = InstructionType.RGBUpdate;
                p.instructions[1].parameters = new object[] { controllerIndex, 0, 255, 0 };

                // ----------------------------------------------------------------------------------------------------------------------------

                // PLAYER LED 1-5 true/false state
                p.instructions[2].type = InstructionType.PlayerLED;
                p.instructions[2].parameters = new object[] { controllerIndex, true, false, true, false, true };

                // ----------------------------------------------------------------------------------------------------------------------------

                // TriggerThreshold needs 2 params p.instructions[0]:0-255 p.instructions[2]:0-255
                p.instructions[3].type = InstructionType.TriggerThreshold;
                p.instructions[3].parameters = new object[] { controllerIndex, Trigger.Right, 0 };

                // ----------------------------------------------------------------------------------------------------------------------------

                Send(p);

                Console.WriteLine("Press any key to send again");
                Console.ReadKey();
            }
        }
        //Maps floats from one range to another.
        public static float Map(float x, float in_min, float in_max, float out_min, float out_max)
        {
            if (x > in_max)
            {
                x = in_max;
            }
            else if (x < in_min)
            {
                x = in_min;
            }
            return (x - in_min) * (out_max - out_min) / (in_max - in_min) + out_min;
        }

        private static DataPacket data = new DataPacket();
        static UdpClient senderClient;
        static IPEndPoint endPoint;
        //Connect to DualSenseX
        static void Connect()
        {
            senderClient = new UdpClient();
            var portNumber = File.ReadAllText(@"C:\Temp\DualSenseX\DualSenseX_PortNumber.txt");
            Console.WriteLine("DSX is using port " + portNumber + ". Attempting to connect..");
            int portNum = settings.DSX_PORT;
            if (portNumber != null)
            {
                try
                {
                    portNum = Convert.ToInt32(portNumber);
                }
                catch (FormatException e)
                {
                    Console.WriteLine($"DSX provided a non numerical Port! Using configured default({settings.DSX_PORT}).");
                    portNum = settings.DSX_PORT;
                }
            }
            else
            {
                Console.WriteLine($"DSX did not provided a port value. Using configured default({settings.DSX_PORT})");
            }

            endPoint = new IPEndPoint(Triggers.localhost, Convert.ToInt32(portNumber));
            try
            {
                senderClient.Connect(endPoint);
            }
            catch (Exception e)
            {
                Console.Write("Error Connecting: ");

                if (e is SocketException)
                {
                    Console.WriteLine("Couldn't Access Port. " + e.Message);
                }
                else if (e is ObjectDisposedException)
                {
                    Console.WriteLine("Connection Object Closed. Restart the Application.");
                }
                else
                {
                    Console.WriteLine("Unknown Error: " + e.Message);
                }
                throw e;
            }
        }
        //Send Data to DualSenseX
        static void Send(Packet data)
        {
            if (verbose)
            {
                Console.WriteLine($"Converting Message to JSON");
            }
            byte[] RequestData = Encoding.ASCII.GetBytes(Triggers.PacketToJson(data));
            if (verbose)
            {
                Console.WriteLine($"{Encoding.ASCII.GetString(RequestData)}");
            }
            try
            {
                if (verbose)
                {
                    Console.WriteLine($"Sending Message to DSX...");
                }
                senderClient.Send(RequestData, RequestData.Length);
                if (verbose)
                {
                    Console.WriteLine($"Message sent to DSX");
                }
            }
            catch (Exception e)
            {
                Console.Write("Error Sending Message: ");

                if (e is SocketException)
                {
                    Console.WriteLine("Couldn't Access Port. " + e.Message);
                    throw e;
                }
                else if (e is ObjectDisposedException)
                {
                    Console.WriteLine("Connection closed. Restarting...");
                    Connect();
                }
                else
                {
                    Console.WriteLine("Unknown Error: " + e.Message);

                }

            }
        }

        //Main running thread of program.
        static async Task Main(string[] args)
        {
            IPEndPoint ipEndPoint = null;
            UdpClient client = null;
            StreamWriter writer = null;
            CsvWriter csv = null;
            try
            {
                for (int i = 0; i < args.Length; i++)

                {
                    string arg = args[i];

                    switch (arg)
                    {
                        case "-v":
                            {
                                Console.WriteLine($"ForzaDSX Version {VERSION}");
                                return;
                            }
                        case "--verbose":
                            {
                                Console.WriteLine("Verbose Mode Enabled!");
                                verbose = true;
                                break;
                            }
                        case "--csv":
                            {
                                logToCsv = true;
                                i++;
                                if (i >= args.Length)
                                {
                                    Console.WriteLine("No Path Entered for Csv file output!! Exiting");
                                    return;
                                }
                                csvFileName = args[i];
                                break;
                            }
                        default:
                            {

                                break;
                            }
                    }
                }
                // Build a config object, using env vars and JSON providers.
                IConfiguration config = new ConfigurationBuilder()
                    .AddIniFile("appsettings.ini")
                    .Build();
                try
                {
                    // Get values from the config given their key and their target type.
                    settings = config.Get<Settings>();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Invalid Configuration File!");
                    Console.WriteLine(e.Message);
                    return;
                }
                if (!settings.DISABLE_APP_CHECK)
                {
                    int forzaProcesses = Process.GetProcessesByName("ForzaHorizon 5").Length;
                    forzaProcesses += Process.GetProcessesByName("ForzaHorizon4").Length;
                    forzaProcesses += Process.GetProcessesByName("ForzaMotorsport7").Length;
                    Process[] DSX = Process.GetProcessesByName("DSX");
                    Process[] DSX_2 = Process.GetProcessesByName("DualsenseX");
                    Process[] cur = Process.GetProcesses();
                    while (forzaProcesses == 0 || DSX.Length+DSX_2.Length == 0)
                    {
                        if (forzaProcesses == 0)
                        {
                            Console.WriteLine("No Running Instances of Forza found. Waiting... ");

                        }
                        else if (DSX.Length == 0)
                        {
                            Console.WriteLine("No Running Instances of DualSenseX found. Waiting... ");
                        }
                        System.Threading.Thread.Sleep(1000);
                        forzaProcesses += Process.GetProcessesByName("ForzaHorizon5").Length;
                        forzaProcesses += Process.GetProcessesByName("ForzaHorizon4").Length; //Guess at name
                        forzaProcesses += Process.GetProcessesByName("ForzaMotorsport7").Length; //Guess at name
                        // DSX = "DSX" or "DualSenseX"
                        DSX = Process.GetProcessesByName("DSX");
                        DSX_2 = Process.GetProcessesByName("DualsenseX");
                    }
                    Console.WriteLine("Forza and DSX are running. Let's Go!");
                }

                settings.LEFT_TRIGGER_EFFECT_INTENSITY = Math.Clamp(settings.LEFT_TRIGGER_EFFECT_INTENSITY, 0.0f, 1.0f);
				settings.RIGHT_TRIGGER_EFFECT_INTENSITY = Math.Clamp(settings.RIGHT_TRIGGER_EFFECT_INTENSITY, 0.0f, 1.0f);

				//Connect to DualSenseX
				Connect();

                //Connect to Forza
                ipEndPoint = new IPEndPoint(IPAddress.Loopback, settings.FORZA_PORT);
                client = new UdpClient(settings.FORZA_PORT);

                Console.WriteLine($"The Program is running. Please set the Forza data out to 127.0.0.1, port {settings.FORZA_PORT} and verify the DualSenseX UDP Port is set to {settings.DSX_PORT}");
                UdpReceiveResult receive;
                if (logToCsv)
                {
                    try
                    {
                        writer = new StreamWriter(csvFileName);
                        csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                        csv.WriteHeader<CsvData>();
                        csv.NextRecord();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Failed to open csv File output. Ensure it is a valid path!");
                        throw e;
                    }
                }

                int count = 0;

                //Main loop, go until killed
                while (true)
                {
                    //If Forza sends an update
                    receive = await client.ReceiveAsync();
                    if (verbose)
                    {
                        Console.WriteLine("recieved Message from Forza!");
                    }
                    //parse data
                    var resultBuffer = receive.Buffer;
                    if (!AdjustToBufferType(resultBuffer.Length))
                    {
                        //  return;
                    }
                    data = ParseData(resultBuffer);
                    if (verbose)
                    {
                        Console.WriteLine("Data Parsed");
                    }

                    //Process and send data to DualSenseX
                    SendData(data, csv);
                    if (logToCsv && count++ > CSV_BUFFER_LENGTH)
                    {
                        writer.Flush();
                        count = 0;
                    }
                }


            }
            catch (Exception e)
            {
                Console.WriteLine("Application encountered an exception: " + e.Message);
            }
            finally
            {
                if (verbose)
                {
                    Console.WriteLine($"Cleaning Up");
                }
                if (client != null)
                {
                    client.Close();
                    client.Dispose();
                }
                if (senderClient != null)
                {
                    senderClient.Close();
                    senderClient.Dispose();
                }
                if (csv != null)
                {
                    csv.Dispose();
                }
                if (writer != null)
                {
                    writer.Flush();
                    writer.Close();

                }

                if (verbose)
                {
                    Console.WriteLine($"Cleanup Finished. Exiting...");
                }

            }
            return;

        }

        static float EWMA(float input, float last, float alpha)
        {
            return (alpha * input) + (1 - alpha) * last;
        }
        static int EWMA(int input, int last, float alpha)
        {
            return (int)Math.Floor(EWMA((float)input, (float)last, alpha));
        }

        //Parses data from Forza into a DataPacket
        static DataPacket ParseData(byte[] packet)
        {
            DataPacket data = new DataPacket();

            // sled
            data.IsRaceOn = packet.IsRaceOn();
            data.TimestampMS = packet.TimestampMs();
            data.EngineMaxRpm = packet.EngineMaxRpm();
            data.EngineIdleRpm = packet.EngineIdleRpm();
            data.CurrentEngineRpm = packet.CurrentEngineRpm();
            data.AccelerationX = packet.AccelerationX();
            data.AccelerationY = packet.AccelerationY();
            data.AccelerationZ = packet.AccelerationZ();
            data.VelocityX = packet.VelocityX();
            data.VelocityY = packet.VelocityY();
            data.VelocityZ = packet.VelocityZ();
            data.AngularVelocityX = packet.AngularVelocityX();
            data.AngularVelocityY = packet.AngularVelocityY();
            data.AngularVelocityZ = packet.AngularVelocityZ();
            data.Yaw = packet.Yaw();
            data.Pitch = packet.Pitch();
            data.Roll = packet.Roll();
            data.NormalizedSuspensionTravelFrontLeft = packet.NormSuspensionTravelFl();
            data.NormalizedSuspensionTravelFrontRight = packet.NormSuspensionTravelFr();
            data.NormalizedSuspensionTravelRearLeft = packet.NormSuspensionTravelRl();
            data.NormalizedSuspensionTravelRearRight = packet.NormSuspensionTravelRr();
            data.TireSlipRatioFrontLeft = packet.TireSlipRatioFl();
            data.TireSlipRatioFrontRight = packet.TireSlipRatioFr();
            data.TireSlipRatioRearLeft = packet.TireSlipRatioRl();
            data.TireSlipRatioRearRight = packet.TireSlipRatioRr();
            data.WheelRotationSpeedFrontLeft = packet.WheelRotationSpeedFl();
            data.WheelRotationSpeedFrontRight = packet.WheelRotationSpeedFr();
            data.WheelRotationSpeedRearLeft = packet.WheelRotationSpeedRl();
            data.WheelRotationSpeedRearRight = packet.WheelRotationSpeedRr();
            data.WheelOnRumbleStripFrontLeft = packet.WheelOnRumbleStripFl();
            data.WheelOnRumbleStripFrontRight = packet.WheelOnRumbleStripFr();
            data.WheelOnRumbleStripRearLeft = packet.WheelOnRumbleStripRl();
            data.WheelOnRumbleStripRearRight = packet.WheelOnRumbleStripRr();
            data.WheelInPuddleDepthFrontLeft = packet.WheelInPuddleFl();
            data.WheelInPuddleDepthFrontRight = packet.WheelInPuddleFr();
            data.WheelInPuddleDepthRearLeft = packet.WheelInPuddleRl();
            data.WheelInPuddleDepthRearRight = packet.WheelInPuddleRr();
            data.SurfaceRumbleFrontLeft = packet.SurfaceRumbleFl();
            data.SurfaceRumbleFrontRight = packet.SurfaceRumbleFr();
            data.SurfaceRumbleRearLeft = packet.SurfaceRumbleRl();
            data.SurfaceRumbleRearRight = packet.SurfaceRumbleRr();
            data.TireSlipAngleFrontLeft = packet.TireSlipAngleFl();
            data.TireSlipAngleFrontRight = packet.TireSlipAngleFr();
            data.TireSlipAngleRearLeft = packet.TireSlipAngleRl();
            data.TireSlipAngleRearRight = packet.TireSlipAngleRr();
            data.TireCombinedSlipFrontLeft = packet.TireCombinedSlipFl();
            data.TireCombinedSlipFrontRight = packet.TireCombinedSlipFr();
            data.TireCombinedSlipRearLeft = packet.TireCombinedSlipRl();
            data.TireCombinedSlipRearRight = packet.TireCombinedSlipRr();
            data.SuspensionTravelMetersFrontLeft = packet.SuspensionTravelMetersFl();
            data.SuspensionTravelMetersFrontRight = packet.SuspensionTravelMetersFr();
            data.SuspensionTravelMetersRearLeft = packet.SuspensionTravelMetersRl();
            data.SuspensionTravelMetersRearRight = packet.SuspensionTravelMetersRr();
            data.CarOrdinal = packet.CarOrdinal();
            data.CarClass = packet.CarClass();
            data.CarPerformanceIndex = packet.CarPerformanceIndex();
            data.DrivetrainType = packet.DriveTrain();
            data.NumCylinders = packet.NumCylinders();

            // dash
            data.PositionX = packet.PositionX();
            data.PositionY = packet.PositionY();
            data.PositionZ = packet.PositionZ();
            data.Speed = packet.Speed();
            data.Power = packet.Power();
            data.Torque = packet.Torque();
            data.TireTempFl = packet.TireTempFl();
            data.TireTempFr = packet.TireTempFr();
            data.TireTempRl = packet.TireTempRl();
            data.TireTempRr = packet.TireTempRr();
            data.Boost = packet.Boost();
            data.Fuel = packet.Fuel();
            data.Distance = packet.Distance();
            data.BestLapTime = packet.BestLapTime();
            data.LastLapTime = packet.LastLapTime();
            data.CurrentLapTime = packet.CurrentLapTime();
            data.CurrentRaceTime = packet.CurrentRaceTime();
            data.Lap = packet.Lap();
            data.RacePosition = packet.RacePosition();
            data.Accelerator = packet.Accelerator();
            data.Brake = packet.Brake();
            data.Clutch = packet.Clutch();
            data.Handbrake = packet.Handbrake();
            data.Gear = packet.Gear();
            data.Steer = packet.Steer();
            data.NormalDrivingLine = packet.NormalDrivingLine();
            data.NormalAiBrakeDifference = packet.NormalAiBrakeDifference();

            return data;
        }

        //Support different standards
        static bool AdjustToBufferType(int bufferLength)
        {
            switch (bufferLength)
            {
                case 232: // FM7 sled
                    return false;
                case 311: // FM7 dash
                    FMData.BufferOffset = 0;
                    return true;
                case 324: // FH4
                    FMData.BufferOffset = 12;
                    return true;
                default:
                    return false;
            }
        }


    }

    //Needed to communicate with DualSenseX
    public static class Triggers
    {
        public static IPAddress localhost = new IPAddress(new byte[] { 127, 0, 0, 1 });

        public static string PacketToJson(Packet packet)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(packet);
        }

        public static Packet JsonToPacket(string json)
        {
            return JsonConvert.DeserializeObject<Packet>(json);
        }
    }

    //The different trigger Modes. These correlate the values in the DualSenseX UI
    public enum TriggerMode
    {
        Normal = 0,
        GameCube = 1,
        VerySoft = 2,
        Soft = 3,
        Hard = 4,
        VeryHard = 5,
        Hardest = 6,
        Rigid = 7,
        VibrateTrigger = 8,
        Choppy = 9,
        Medium = 10,
        VibrateTriggerPulse = 11,
        CustomTriggerValue = 12,
        Resistance = 13,
        Bow = 14,
        Galloping = 15,
        SemiAutomaticGun = 16,
        AutomaticGun = 17,
        Machine = 18
    }

    //Custom Trigger Values. These correspond to the values in the DualSenseX UI
    public enum CustomTriggerValueMode
    {
        OFF = 0,
        Rigid = 1,
        RigidA = 2,
        RigidB = 3,
        RigidAB = 4,
        Pulse = 5,
        PulseA = 6,
        PulseB = 7,
        PulseAB = 8,
        VibrateResistance = 9,
        VibrateResistanceA = 10,
        VibrateResistanceB = 11,
        VibrateResistanceAB = 12,
        VibratePulse = 13,
        VibratePulseA = 14,
        VibratePulsB = 15,
        VibratePulseAB = 16
    }

    public enum Trigger
    {
        Invalid,
        Left,
        Right
    }

    public enum InstructionType
    {
        Invalid,
        TriggerUpdate,
        RGBUpdate,
        PlayerLED,
        TriggerThreshold
    }

    public struct Instruction
    {
        public InstructionType type;
        public object[] parameters;
    }

    public class Packet
    {
        public Instruction[] instructions;
    }
}

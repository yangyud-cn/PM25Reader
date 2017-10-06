using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;

namespace Reader
{
    class Program
    {
        protected static void MyBreakHandler(object sender, ConsoleCancelEventArgs args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;

            Console.WriteLine(" *** Break ***");
            Console.WriteLine("Exited per request from user ...");
            Environment.Exit(0);
        }

        static void ShowMenu()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.BackgroundColor = ConsoleColor.Black;

            Console.WriteLine("---  command ------------------");
            Console.WriteLine("p : toggle passive/active mode");
            Console.WriteLine("s : toggle standby/work mode");
            Console.WriteLine("r : read in passive mode");
            Console.WriteLine("x : exit");
            Console.WriteLine("-------------------------------");
        }

        static void Main(string[] args)
        {
            Console.Clear();

            Console.CancelKeyPress += new ConsoleCancelEventHandler(MyBreakHandler);
            bool keepRun = true;
            while (keepRun)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.DarkCyan;
                string pms5003tPort = null;
                Console.WriteLine("Probing Available Ports:");
                foreach (string sp in SerialPort.GetPortNames())
                {
                    bool found = Sensor.PMS5003T.ProbePort(sp);
                    Console.WriteLine("   {0} : {1}", sp, found ? "PMS5003T" : "---");
                    if (found)
                    {
                        pms5003tPort = sp;
                        break;
                    }
                }

                if (pms5003tPort == null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.BackgroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine("* Sensor not found, delay 5 seconds to retry ... ");
                    Thread.Sleep(5000);
                    continue;
                }

                ShowMenu();

                using (var sensor = new Sensor.PMS5003T(pms5003tPort))
                {
                    bool passiveMode = false;
                    bool standbyMode = false;

                    Sensor.SensorData data = null;
                    bool dataReadOK = false;
                    int retry = 0;
                    int maxRetry = 2;
                    while (retry < maxRetry && keepRun)
                    {
                        ConsoleKeyInfo key = new ConsoleKeyInfo();
                        bool keyPressed = false;

                        if (passiveMode || standbyMode)
                        {
                            ShowMenu();
                            Console.Write("> ");
                            key = Console.ReadKey(true);
                            keyPressed = true;
                        }
                        else
                        {
                            if (Console.KeyAvailable)
                            {
                                key = Console.ReadKey(true);
                                keyPressed = true;
                            }
                        }

                        if (keyPressed)
                        {
                            if ((key.Modifiers & (ConsoleModifiers.Alt | ConsoleModifiers.Control)) == 0)
                            {
                                switch (key.KeyChar)
                                {
                                    case 'p':
                                    case 'P':
                                        passiveMode = !passiveMode;
                                        Console.WriteLine(passiveMode ? "Enter Passive Read Mode" : "Enter Active Read Mode");
                                        sensor.SetReadMode(passiveMode);
                                        continue;
                                    case 's':
                                    case 'S':
                                        standbyMode = !standbyMode;
                                        Console.WriteLine(standbyMode ? "Enter Standby Mode" : "Enter Active Mode");
                                        sensor.SetStandbyMode(standbyMode);
                                        if (!standbyMode)
                                        {
                                            // it will switch back to active mode when leaving standby mode
                                            passiveMode = false;
                                        }
                                        continue;
                                    case 'r':
                                    case 'R':
                                        if (standbyMode)
                                        {
                                            Console.WriteLine("Can't read in standby mode");
                                            continue;
                                        }

                                        if (passiveMode)
                                        {
                                            dataReadOK = sensor.PassiveRead(out data);
                                            break;
                                        }
                                        else
                                        {
                                            continue;
                                        }
                                    case 'x':
                                    case 'X':
                                        keepRun = false;
                                        Console.WriteLine("Exitting ...");
                                        continue;
                                }
                            }
                        }

                        if (!passiveMode && !standbyMode)
                        {
                            dataReadOK = sensor.ReadData(out data);
                        }

                        if (!dataReadOK)
                        {
                            retry++;
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.BackgroundColor = ConsoleColor.DarkCyan;
                            Console.WriteLine("* Failed to read data, retry {0} / {1} ...", retry, maxRetry);
                            continue;
                        }

                        retry = 0;  // reset retry counter
                       
                        if (data.PM2_5 < 50)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.BackgroundColor = ConsoleColor.Black;
                        }
                        else if (data.PM2_5 < 100)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.BackgroundColor = ConsoleColor.Black;
                        }
                        else if (data.PM2_5 < 150)
                        {
                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.BackgroundColor = ConsoleColor.Black;
                        }
                        else if (data.PM2_5 < 200)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.BackgroundColor = ConsoleColor.Black;
                        }
                        else if (data.PM2_5 < 300)
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.BackgroundColor = ConsoleColor.DarkMagenta;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.BackgroundColor = ConsoleColor.DarkRed;
                        }

                        Console.WriteLine("PM1.0 = {0}, PM2.5 = {1}, PM10 = {2}", data.PM1_0, data.PM2_5, data.PM10);
                        Console.WriteLine("PM1.0A = {0}, PM2.5A = {1}, PM10A = {2}", data.PM1_0A, data.PM2_5A, data.PM10A);
                        Console.WriteLine("Count0.3 = {0}, Count0.5 = {1}, Count1.0 = {2}, Count2.5 = {3}",
                            data.Count0_3, data.Count0_5, data.Count1_0, data.Count2_5);
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.BackgroundColor = ConsoleColor.Black;
                        Console.WriteLine("Temporature = {0}C, Humidity = {1}%", data.Temporature, data.Humidity);
                        Console.WriteLine();
                    }

                    if (keepRun)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.BackgroundColor = ConsoleColor.DarkCyan;
                        Console.WriteLine("* Failed to read data, restart sensor probing ...");
                    }
                }
            }
        }
    }
}

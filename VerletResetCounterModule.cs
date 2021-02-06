using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace MaDDTwitchBot
{
    class VerletResetCounterModule
    { /// <summary>
      /// Read Process.
      /// </summary>
        const int PROCESS_WM_READ = 0x0010;

        /// <summary>
        /// Hook into a process.
        /// </summary>
        /// <param name="dwDesiredAccess">The Access type we're working with.</param>
        /// <param name="bInheritHandle">Whether to inherit the handle</param>
        /// <param name="dwProcessId">The PID of the process.</param>
        /// <returns>An IntPtr to the process base address.</returns>
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        /// <summary>
        /// Read a set of bytes from the process at a specific Address in memory.
        /// </summary>
        /// <param name="hProcess">IntPtr to the process base address.</param>
        /// <param name="lpBaseAddress">IntPtr to the place in memory we're reading from.</param>
        /// <param name="lpBuffer">byte[] to fill into.</param>
        /// <param name="dwSize">Number of bytes to read.</param>
        /// <param name="lpNumberOfBytesRead">The number of bytes to read.</param>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(IntPtr hProcess,
        IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        /// <summary>
        /// The offset from mono.dll to apply.
        /// </summary>
        private static readonly int monoOffset = 0x264110;

        /// <summary>
        /// The pointer chain to the verlet swing level timer.
        /// </summary>
        private static readonly int[] timerOffsets = new int[] { 0x340, 0x2c };

        /// <summary>
        /// The pointer chain to the verlet swing number timer.
        /// </summary>
        private static readonly int[] levelOffsets = new int[] { 0xa90, 0x92c };

        /// <summary>
        /// The process we're operating on.
        /// </summary>
        private static Process verletProcess;

        /// <summary>
        /// The base address of Verlet Swing.exe
        /// </summary>
        private static IntPtr processHandle;

        /// <summary>
        /// The base address of mono.dll
        /// </summary>
        private static IntPtr monoBase;

        /// <summary>
        /// On each cycle, store the time of the level timer as of the last cycle.
        /// </summary>
        private static double lastTime = 0;

        /// <summary>
        /// On each cycle, store the level number as of the last cycle.
        /// </summary>
        private static int lastLevel = 0;

        /// <summary>
        /// Every time the level timer decreases (minus switch bounce) we increment the retry count.
        /// </summary>
        private static int retryCount = 0;

        /// <summary>
        /// Track whether Verlet is open or closed.
        /// </summary>
        public static bool isVerletOpen = false;

        // private static DateTime levelStartGrindTime;

        /// <summary>
        /// On each cycle, store the level number as of the last cycle.
        /// </summary>
        public static int LastLevel { get => lastLevel; set => lastLevel = value; }

        /// <summary>
        /// On each cycle, store the time of the level timer as of the last cycle.
        /// </summary>
        public static double LastTime { get => lastTime; set => lastTime = value; }

        /// <summary>
        /// Every time the level timer decreases (minus switch bounce) we increment the retry count.
        /// </summary>
        public static int RetryCount { get => retryCount; set => retryCount = value; }

        /// <summary>
        /// Event to hook onto that signals that the game has changed level.
        /// </summary>
        public static event EventHandler OnLevelChange;

        /// <summary>
        /// Hook into Verlet Swing, and initiate the rest of the calls.
        /// </summary>
        public static void Init()
        {
            // Get the processes by the name of Verlet Swing (Either will be 0 or 1.)
            Process[] processList = Process.GetProcessesByName("Verlet Swing");

            // If Verlet is open, continue.
            if (processList.Length > 0)
            {
                isVerletOpen = true;
                verletProcess = processList[0];
                verletProcess.Exited += OnVerletClose;

                // Open the Process for reading, and also get all of the modules Verlet Swing is using.
                ProcessModuleCollection bModules = verletProcess.Modules;
                processHandle = OpenProcess(PROCESS_WM_READ, false, verletProcess.Id);

                // Iterate through the modules, we're looking for mono.dll in particular.
                foreach (ProcessModule module in bModules)
                {
                    if (module.ModuleName == "mono.dll")
                    {
                        monoBase = module.BaseAddress;
                        break;
                    }
                }
            }
            else
            {
                // Verlet Swing isn't open at the moment.
                // TODO: Find something to do here instead.
                isVerletOpen = false;
            }
        }

        private static void OnVerletClose(object sender, EventArgs e)
        {
            isVerletOpen = false;
        }

        public static void CoreLoop()
        {
            // While we're initiating above, if Verlet Swing is not open, VerletProcess is never defined.
            // Ensure that Verlet is Open.
            if (verletProcess != null)
            {
                // See comment on if (time == lasttime && time <0.1)
                bool shouldLock = false;

                while (isVerletOpen)
                {
                    // To reduce cpu usage, do a mini-sleep between cycles, don't need to update every ms when the level timer increments in 0.02s
                    Thread.Sleep(20);

                    // Grab the current level and time from in-game memory.
                    int level = getCurrentLevel();
                    double time = getCurrentTime();

                    if(level != lastLevel)
                    {
                        //levelStartGrindTime = DateTime.Now;
                        OnLevelChange?.Invoke(null, new EventArgs());
                        RetryCount = 0;
                    }
                    else
                    {
                        // Verlet Swing occcasionally puts you at a positive time on reset, (0.05 or less, typically)
                        // If two cycles pass and the in-game time is the same on both, and we're still at the beginning of the level
                        // Lock this cycle, and wait until the timer goes up.
                        // This avoids a level starting at 0.05s - the resetCount incrementing - and then when they player actually begins
                        // and the timer returns on 0.00s then a double resetCount takes place.
                        if (time == LastTime && time < 0.1)
                        {
                            // Hold us in lock until time starts rising.
                            shouldLock = true;
                        }
                        else if (time > LastTime)
                        {
                            shouldLock = false;
                        }

                        // If the time is less than the time on the last cycle (the player did a reset)
                        // Then we've either changed levels or restarted the same level.
                        if (time < LastTime && !shouldLock)
                        {
                            RetryCount++;
                        }
                    }
                    LastLevel = level;
                    LastTime = time;
                }
            }
        }

        /// <summary>
        /// This function returns the address of the next pointer in a pointer chain.
        /// </summary>
        /// <param name="baseLocation"></param>
        /// <param name="offset"></param>
        /// <param name="processHandle"></param>
        /// <returns></returns>
        private static IntPtr GetNextPointerAddressFromLastPointer(IntPtr baseLocation, int offset, IntPtr processHandle)
        {
            IntPtr offsetLocation = IntPtr.Add(baseLocation, offset);
            byte[] buffer = new byte[8]; //To read a 24 byte unicode string

            ReadProcessMemory(processHandle, offsetLocation, buffer, buffer.Length, out _);

            IntPtr newOffset = (IntPtr)BitConverter.ToInt64(buffer);

            return newOffset;
        }

        /// <summary>
        /// Read a 4byte (signed) integer from a base address in memory.
        /// </summary>
        /// <param name="pointerLocation">The Address we're reading from.</param>
        /// <param name="processHandle">The Process we're attached to.</param>
        /// <returns></returns>
        private static byte[] readInt(IntPtr pointerLocation, IntPtr processHandle)
        {
            byte[] buffer = new byte[4];
            ReadProcessMemory(processHandle, pointerLocation, buffer, buffer.Length, out _);
            return buffer;
        }

        /// <summary>
        /// In a typical pointer chain, you might go through 5 pointers before reaching your value
        /// Base + offset[0] -> address of pointer1. pointer1 + offset[1] -> pointer 2...... pointer[5] + offset -> value
        /// </summary>
        /// <param name="pointerOffsets"></param>
        /// <returns>The address of the pointer to the final value.</returns>
        private static IntPtr RunPointerChain(int[] pointerOffsets)
        {
            // First account for the pointer from mono.dll
            IntPtr offsetFromBase = GetNextPointerAddressFromLastPointer(monoBase, monoOffset, processHandle);

            // Calculate the position of the next pointer until you get to the last pointer in the chain.
            IntPtr lastPtr = offsetFromBase;
            for (int i = 0; i < pointerOffsets.Length - 1; i++)
            {
                lastPtr = GetNextPointerAddressFromLastPointer(lastPtr, pointerOffsets[i], processHandle);
            }

            // Return the pointer to the value at the end of the chain.
            return IntPtr.Add(lastPtr, pointerOffsets[^1]);
        }

        /// <summary>
        /// Get the current level from Verlet Swing as stored in memory.
        ///  Automatically add 1 at the end such that it matches the rest of our indexing for levels.
        /// </summary>
        /// <returns>The current level as stored in memory.</returns>
        private static int getCurrentLevel()
        {
            IntPtr lvlPtr = RunPointerChain(levelOffsets);
            byte[] bytesAtLoc = readInt(lvlPtr, processHandle);

            int levelNumber = BitConverter.ToInt32(bytesAtLoc);
            return levelNumber + 1;
        }

        /// <summary>
        /// Gets the current level time as stored in memory.
        /// </summary>
        /// <returns>The current level time as stored in memory.</returns>
        private static double getCurrentTime()
        {
            IntPtr timePtr = RunPointerChain(timerOffsets);
            byte[] bytesAtLoc = readInt(timePtr, processHandle);

            int levelNumber = BitConverter.ToInt32(bytesAtLoc);
            double time = levelNumber * 0.02;
            return time;
        }
    }

}

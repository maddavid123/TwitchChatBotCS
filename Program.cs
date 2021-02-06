using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MaDDTwitchBot
{
    /// <summary>
    /// Opens a Connection to Twitch and creates some base utility for command handling.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The writer to the stream we're operating on.
        /// Write commands go straight to twitch.
        /// </summary>
        private static StreamWriter writer;

        /// <summary>
        /// The reader of the stream we're operating on.
        /// Read messages are coming from twitch.
        /// </summary>
        private static StreamReader reader;

        /// <summary>
        /// The channel our client is watching. This should be all lower case.
        /// </summary>
        private static string channel;

        /// <summary>
        /// Send a Message to the twitch IRC server useing the StreamWriter, encase the message in the ccorrect format and flush afterwards.
        /// </summary>
        /// <param name="message">The message to be sent to Twitch.</param>
        /// <exception cref="ArgumentException">Thrown if message is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the reader or writer have not yet been assinged values.</exception>
        private static void SendMessage(string message)
        {
            // Do some basic null checks.
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException($"'{nameof(message)}' cannot be null or whitespace", nameof(message));
            }
            if (writer == null)
            {
                throw new InvalidOperationException($"'{nameof(writer)}' cannot be null, ensure the TCPStream has been opened.");
            }
            if (reader == null)
            {
                throw new InvalidOperationException($"'{nameof(reader)}' cannot be null, ensure the TCPStream has been opened.");
            }

            // Messages sent to twitch should be as follows PRIVMSG #channelname : message
            // For example PRIVMSG #maddavid123 : This is a Test Message!
            // They should then be ended with a carriage return \r\n and flushed.
            writer.WriteLine("PRIVMSG #" + channel + " :" + message + "\r\n");
            writer.Flush();
        }

        /// <summary>
        /// Process a incoming string coming from twitch.
        /// </summary>
        /// <param name="messageFromTwitch">The raw message we've recieved from twitch</param>
        private static void processTwitchBackMessage(string messageFromTwitch)
        {
            // Every now and then, twitch will PING the client to see if it's still connected.
            // Ensure that when they do, we return the gesture.
            if (messageFromTwitch.Equals("PING :tmi.twitch.tv"))
            {
                writer.WriteLine("PONG :tmi.twitch.tv\r\n");
                writer.Flush();
                Console.WriteLine("Ponging twitch's ping.");
                return; // If we do get a ping, don't try and process any further.
            }

            // Next, split the returned message from twitch into username and message.
            // Returned message typically looks like this:
            // :maddavid123!maddavid123@maddavid123.tmi.twitch.tv PRIVMSG #maddavid123 :!verlet10

            // We can get the username and message by splitting on the colons (:) and taking the split[1] and split[2].
            // Example:
            // 0 -
            // 1 - maddavid123!maddavid123@maddavid123.tmi.twitch.tv PRIVMSG #maddavid123 
            // 2 - !verlet10
            string[] messageSplit = messageFromTwitch.Split(':');

            // Further split on split[1] on the ! symbol to get the username
            // Example
            // 0 - maddavid123 (We want this one!)
            // 1 - !maddavid123@maddavid123.tmi.twitch.tv PRIVMSG #maddavid123
            string username = messageSplit[1].Split('!')[0];
            string message = messageSplit[2];
            
            //Then send it off for extra processing!
            HandleCommand(username, message);
        }

        /// <summary>
        /// Process a chat message in a usable form.
        /// </summary>
        /// <param name="username">Currently unused, but for future operations we can match on a modlist or VIP list.</param>
        /// <param name="message">The message to process.</param>
        private static void HandleCommand(string username, string message)
        {
            // Not sure whether this is particularly desired, but we can keep this in
            Console.WriteLine(username + ":" + message);

            // Check it's a command.
            if (message.StartsWith('!'))
            {
                string command = message.Split(' ')[0]; // Commands typically have a space after them, we can just take the first part.
                string trueCommand = command.Substring(1, command.Length-1); // Drop the first character, and the final one. This will leave us with the whole command.

                // Check whether this is a verlet command.
                if (trueCommand.StartsWith("verlet", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove the "verlet" part of the command, leaving you only with the level number.
                    // Then, parse the number into an integer. Finally, send the Level, level Name and time off to twitch!
                    string levelNumberString = trueCommand.Remove(0, 6);
                    if (int.TryParse(levelNumberString, out int levelNumber))
                    {
                        SendMessage("Level " + levelNumber + " - " + VerletHighScoresParser.levelNames[levelNumber] + ": " + VerletHighScoresParser.levelTimes[levelNumber] + "s!");
                    }
                }
            }
        }

        private static void OnVerletLevelChange(object sender, EventArgs e)
        {
            // Only run if we've already initiated the Verlet Swing XML Parser.
            if(VerletHighScoresParser.levelNames.Count > 0)
            {
                int lastLevel = VerletResetCounterModule.LastLevel;
                int attempts = VerletResetCounterModule.RetryCount;
                string lastLevelName = VerletHighScoresParser.levelNames[lastLevel];
                if (attempts > 0)
                {
                    SendMessage("Level Changed. Attempts on level " + VerletResetCounterModule.LastLevel + " - " + lastLevelName + ": " + attempts);
                }
            }
        }
        private static void OnVerletLevelPersonalBest(object sender, EventArgs e)
        {
            if(VerletHighScoresParser.levelNames.Count > 0)
            {
                double oldTime = VerletHighScoresParser.OldTime;
                double newTime = VerletHighScoresParser.NewTime;
                int levelNum = VerletHighScoresParser.PbLevel;
                double improvement = oldTime - newTime;
                /*
                VerletHighScoresParser.OldTime = 0;
                VerletHighScoresParser.NewTime = 0;
                VerletHighScoresParser.PbLevel = 0;
                */
                SendMessage("Personal Best on Level " + levelNum + " - " + VerletHighScoresParser.levelNames[levelNum] +". Attempt number: " + VerletResetCounterModule.RetryCount);
                SendMessage("From " + oldTime.ToString("#.000") + "s to " + newTime.ToString("#.000") + "s. An improvement of " + improvement.ToString("#0.000") + "s!");
            }
        }

        /// <summary>
        /// Sets up the TCP connection to twitch, and then begins monitoring the connection for back and forth messages
        /// </summary>
        /// <param name="args"> args[0] should be the OAuth token for a given bot account. args[1] should be the username for the bot. args[2] is the channel we're operating in.</param>
        static void Main(string[] args)
        {
            // Set up the HighScore parser, then grab the twitch user information from the incoming command line arguments.
            VerletHighScoresParser.Init();
            string pass = args[0];
            string nick = args[1].ToLower();
            channel = args[2].ToLower();

            // Set up a TCP connection to twitch, and set up a writer/reader for the underlying stream.
            TcpClient client = new TcpClient("irc.twitch.tv", 6667);
            writer = new StreamWriter(client.GetStream());
            reader = new StreamReader(client.GetStream());

            // Connect to the IRC channel of the user. 
            // First, send PASS followed by the bots's OAuth token.
            // Secondly the NICK (The bots username)
            // Finally JOIN #channelName where channelName is the channel we're joining.
            writer.WriteLine("PASS " + pass + "\r\n");
            writer.Flush();
            writer.WriteLine("NICK " + nick + "\r\n");
            writer.Flush();
            writer.WriteLine("JOIN #" + channel + "\r\n");
            writer.Flush();

            // After sending that off, twitch will attempt to join the channel.
            // Several messages are sent from twitch that ensure we're reading and processing the stream correctly.
            // Once we've seen: End of /NAMES list we can begin operating normally.
            bool loading = false;
            string inBuffer = "";
            while(!loading)
            {
                try
                {
                    inBuffer += reader.ReadLine();
                }
                catch (Exception e)
                {
                    // HACKHACK - Exception e is way too broad and is just a catch all. Specify a better exception set.
                    // BUGBUG - If for any reason the TCPClient connecting to that host on that port is already open it'll spam the console to all hell.
                    Console.WriteLine(e.Message);
                }
                string[] lines = inBuffer.Split("\n");
                foreach(string s in lines)
                {
                    loading = s.Contains("End of /NAMES list");
                }
            }

            // Now that we've connected, we may as well let the twitch chat know we've arrived.
            // Then keep reading lines in from Twitch and process them.
            SendMessage("Twitch Bot: Online");

            // Hook into Verlet Swing if it's running.
            VerletResetCounterModule.Init();
            Task runVerletMemoryMonitorTask = Task.Factory.StartNew(() => VerletResetCounterModule.CoreLoop());

            VerletResetCounterModule.OnLevelChange += OnVerletLevelChange;
            VerletHighScoresParser.OnLevelPersonalBest += OnVerletLevelPersonalBest;

            // Currently not accounting for network dropout.
            bool connectionOpen = true;
            DateTime lastAttempt = DateTime.Now;
            while (connectionOpen)
            {
                // Every x seconds if Verlet is closed, try and connect.
                if(!VerletResetCounterModule.isVerletOpen)
                {
                    DateTime now = DateTime.Now;
                    TimeSpan time = now.Subtract(lastAttempt);
                    if (time.TotalSeconds > 30)
                    {
                        VerletResetCounterModule.Init();
                        lastAttempt = now;
                        Console.WriteLine("Trying to connect to Verlet!");
                    }
                }
                if (((NetworkStream)reader.BaseStream).DataAvailable)
                {
                    while ((inBuffer = reader.ReadLine()) != null)
                    {
                        processTwitchBackMessage(inBuffer);
                    }
                }
            }
        }
    }
}

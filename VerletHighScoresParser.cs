using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MaDDTwitchBot
{
    /// <summary>
    /// Reads an XML File containing the times that the local user has in the game Verlet Swing, and updates every time that file is updated.
    /// </summary>
    internal class VerletHighScoresParser
    {
        /// <summary>
        /// A List containing the times for each level in the game.
        /// </summary>
        public static List<double> levelTimes = new List<double>();

        /// <summary>
        /// A list containing the name of each level in the game.
        /// </summary>
        public static readonly List<string> levelNames = new List<string>();

        /// <summary>
        /// Sets up a File Watcher to fire events whenever a certain file is changed. In this case highscore.xml.
        /// </summary>
        private static readonly FileSystemWatcher watcher = new FileSystemWatcher();

        /// <summary>
        /// The File Location of the Highscore.xml file. 
        /// Typically: C:\Users\CurrentUser\%appdata%\LocalLow\Flamebait Games\Verlet Swing
        /// </summary>
        public static string fileLocation = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"Low\Flamebait Games\Verlet Swing";

        private static double oldTime = 0;
        private static double newTime = 0;
        private static int pbLevel = 0;

        /// <summary>
        /// Gets or Sets the old level time.
        /// </summary>
        public static double OldTime { get => oldTime; set => oldTime = value; }

        /// <summary>
        /// Gets or Sets the new level time.
        /// </summary>
        public static double NewTime { get => newTime; set => newTime = value; }

        /// <summary>
        /// Gets or Sets the level that just PB'd.
        /// </summary>
        public static int PbLevel { get => pbLevel; set => pbLevel = value; }

        /// <summary>
        /// Event to hook onto to notify on Personal Best.
        /// </summary>
        public static event EventHandler OnLevelPersonalBest;


        /// <summary>
        /// Initiate the File Parser, and set up this utility class.
        /// </summary>
        public static void Init()
        {
            // Add unimportant data to element 0.
            // Then add the name of every level to the levelNames List and run our first parse of the highscore.xml file.
            levelNames.Add(string.Empty);
            populateLevelNameList();
            ReadScores();

            // Set up the FileWatcher to trigger every time highscore.xml is updated.
            watcher.Path = fileLocation;
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Filter = "highscore.xml";
            watcher.Changed += OnHighScoreFileUpdate;
            watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Populate the levelNames list using a hub function. The following functions are all probably optimised together.
        /// But I believe this to be more slightly more organised.
        /// </summary>
        private static void populateLevelNameList()
        {
            populateCheckmateHistory();
            populateMobymart();
            populateNimisNonLaute();
            populateWondercon1998();
            populateCrimsonCourt();
        }

        /// <summary>
        /// Populate the levelNames list with the names in World 1: Checkmate History.
        /// </summary>
        private static void populateCheckmateHistory()
        {
            levelNames.Add("Swing");
            levelNames.Add("Air");
            levelNames.Add("Tutanchess");
            levelNames.Add("Roman Vanilla");
            levelNames.Add("Canyon");
            levelNames.Add("Rocket Town");
            levelNames.Add("Pizza Dreams");
            levelNames.Add("Around The Block");
            levelNames.Add("Mausoleum");
            levelNames.Add("Attack on Pillar");
            levelNames.Add("Time Freezer");
            levelNames.Add("Horizontality");
            levelNames.Add("Ancient Ben");
            levelNames.Add("Pillarloops");
            levelNames.Add("Dynamic Discs");
            levelNames.Add("Needles");
            levelNames.Add("Sidewinder");
            levelNames.Add("Easter Bridge");
            levelNames.Add("Lefty Right");
            levelNames.Add("Maze Runner");
        }

        /// <summary>
        /// Populate the levelNames list with the names in World 2: Mobymart.
        /// </summary>
        private static void populateMobymart()
        {
            levelNames.Add("Bubbles");
            levelNames.Add("Dolphin Plumber");
            levelNames.Add("Space Buoys");
            levelNames.Add("Ice Hoops");
            levelNames.Add("Through The Weeds");
            levelNames.Add("Bubble Trouble");
            levelNames.Add("Tunnel");
            levelNames.Add("Two Towers");
            levelNames.Add("Rafts");
            levelNames.Add("Elevator");
            levelNames.Add("High Ground");
            levelNames.Add("Upstream");
            levelNames.Add("Gear Up");
            levelNames.Add("Time Trial");
            levelNames.Add("Foot Corals");
            levelNames.Add("Shifters");
            levelNames.Add("Dank Dolphins");
            levelNames.Add("Clockwork");
            levelNames.Add("Tick Tock Clock");
            levelNames.Add("Tunnel II");
        }

        /// <summary>
        /// Populate the levelNames list with the names in World 3: Nimis Non Laute
        /// </summary>
        private static void populateNimisNonLaute()
        {
            levelNames.Add("Face It");
            levelNames.Add("Table Flippers");
            levelNames.Add("Burger Flippers");
            levelNames.Add("Meaty Slingers");
            levelNames.Add("Neatballs");
            levelNames.Add("Meaty Plates");
            levelNames.Add("Jumpy Jumpy Pizza");
            levelNames.Add("Sharp Cheddar");
            levelNames.Add("Catch A Ride!");
            levelNames.Add("Cream Sauce");
            levelNames.Add("French Hotdog");
            levelNames.Add("Burger Tower");
            levelNames.Add("Pizza Tunnel");
            levelNames.Add("Rocketballs");
            levelNames.Add("Mount Yuumy");
            levelNames.Add("Surf 'n Turf");
            levelNames.Add("Hamburglar");
            levelNames.Add("Pizza Valley");
            levelNames.Add("Keep Your Head Down");
            levelNames.Add("Waste Disposal");
        }

        /// <summary>
        /// Populate the levelNames list with the names in World 4: Wondercon 1998
        /// </summary>
        private static void populateWondercon1998()
        {
            levelNames.Add("Lefties");
            levelNames.Add("Truth");
            levelNames.Add("Memory Access");
            levelNames.Add("Render Distance");
            levelNames.Add("Rage Quit");
            levelNames.Add("Spears");
            levelNames.Add("Data Mining");
            levelNames.Add("Aroud the Corner");
            levelNames.Add("EJECT");
            levelNames.Add("Verticality");
            levelNames.Add("Spears II");
            levelNames.Add("Singularity");
            levelNames.Add("Quick Maths");
            levelNames.Add("But can you reach this");
            levelNames.Add("Boxings");
            levelNames.Add("pillartower");
            levelNames.Add("Enter Damnation!");
            levelNames.Add("Ziggin' 'n' a Zaggin'");
            levelNames.Add("Spiraling Away");
            levelNames.Add("Tunnel III");
        }

        /// <summary>
        /// Populate the levelNames list with the names in World 5: Crimson Court
        /// </summary>
        private static void populateCrimsonCourt()
        {
            levelNames.Add("Bare Bones");
            levelNames.Add("The Missing Link");
            levelNames.Add("Collapse");
            levelNames.Add("Eye of the Beast");
            levelNames.Add("Kingdom");
            levelNames.Add("Sticks and a Hole");
            levelNames.Add("Thorns");
            levelNames.Add("Spears III");
            levelNames.Add("CROWND");
            levelNames.Add("The Depths");
            levelNames.Add("The Climb");
            levelNames.Add("Rock Skipping");
            levelNames.Add("Julbock");
            levelNames.Add("Crevice");
            levelNames.Add("Doom");
            levelNames.Add("Rage");
            levelNames.Add("Spiraling to the End");
            levelNames.Add("Dante's Slope");
            levelNames.Add("Elevator II");
            levelNames.Add("Sorry");
        }

        /// <summary>
        /// Use LINQ to XML to read the highscore.xml file and grab the times from the file.
        /// </summary>
        private static void ReadScores()
        {
            // BUGBUG :  IOException on High Score update.
            // HACKHACK: Try to sleep for half a second prior to updating our level time.
            Thread.Sleep(500);
            List<double> newLevelTimes = new List<double>();
            XElement scoreList = XElement.Load(fileLocation + @"\highscore.xml");
            // TODO: LINQ here can be simplified/optimised.
            var highScoreList = from highscoreList in scoreList.Elements() select highscoreList;
            var highscores = from highscore in highScoreList.Elements() select highscore;
            int i = 0;
            foreach (var s in highscores)
            {
                if (double.TryParse(s.Attribute("time").Value, out double time))
                {
                    newLevelTimes.Add(time / 1000);

                    // If there's an old time  stored, and this update has beaten the old time
                    if(levelTimes.Count > 0 && (time/1000) < levelTimes[i])
                    {
                        // This is a new local PB.
                        PbLevel = i;
                        OldTime = levelTimes[i];
                        NewTime = time/1000;
                        OnLevelPersonalBest?.Invoke(null, new EventArgs());
                        
                    }
                }
                i++;
            }
            levelTimes = newLevelTimes;
        }

        /// <summary>
        /// Function callback for when the Highscore.xml file is changed.
        /// </summary>
        /// <param name="source">Unused</param>
        /// <param name="e">Unused.</param>
        private static void OnHighScoreFileUpdate(object source, FileSystemEventArgs e)
        {
            ReadScores();
        }

    }
}

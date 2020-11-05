/** DrillingRig.cs v. 0.31

	Drilling program for Space Engineers.
	Author: mkazin
	Source: https://github.com/mkazin/SpaceEngineers/

	Description: this program will drill an area extending away from a drill rig.
**/

// Number of times a rotor will swing in an arc across the current distance
// Has a linear relationship with rotor RPM.
// Do not lower below two, which allows for distance piston to retract.
// Setting your rotor at above 2RPM will increase the chances of the drill arm getting stuck.
const int MAX_ROTOR_SWINGS = 2;

// Amount to increse the distance piston after we finish our swings in meters.
const float DISTANCE_DELTA = 2.0f;

// Amount to raise drill pistons/lower lift pistons between swing sets.
const float HEIGHT_DELTA = 0.4f;

// Constants which differ per drill
const string PREFIX_DRILL_NAME = "Drill";
const string PREFIX_DRILL_PISTON = "Drill Piston";
const string PREFIX_LIFT_PISTON = "Lift Piston";

// Used to store position state
const char DELIMITER_GROUP = ';';
const char DELIMITER_ITEM = ':';

/*** Some pre-defined drilling rigs I've set up ***/

/*** Magnesium Mine ****
private static List<string> NAME_DRILL_PISTONS = new List<String>() {
	"Drill Piston - 1st",
	"Drill Piston - 2nd",
	"Drill Piston - 3rd"};
private static List<string> NAME_LIFT_PISTONS = new List<String>() {
	"Drill - Lift Piston - Lower",
	"Drill - Lift Piston - Upper" };
const string NAME_DISTANCE_PISTON = "Drill - Distance Piston";
const string NAME_ROTOR = "Drill - Advanced Rotor";
const string NAME_LCD = "Drill - LCD";
/**/

/*** Money Pit (gold & silver) ****
private static List<string> NAME_DRILL_PISTONS = new List<String>() {
	"Drill Piston - 1st",
	"Drill Piston - 2nd",
	"Drill Piston - 3rd",
	"Drill Piston - 4th" };
private static List<string> NAME_LIFT_PISTONS = new List<String>() {
	"Lift Piston - Lower",
	"Lift Piston - Upper" };
const string NAME_DISTANCE_PISTON = "Distance Piston";
const string NAME_ROTOR = "Drill Rotor";
const string NAME_LCD = "Drill LCD";
/**/

/*** Silver Drill (HQ) ***
private static List<string> NAME_DRILL_PISTONS = new List<String>() {
	"Silver Drill - Lower Drill Piston",
	"Silver Drill - Upper Drill Piston"};
private static List<string> NAME_LIFT_PISTONS = new List<String>() {
	"Silver Drill - Lift Piston" };
const string NAME_DISTANCE_PISTON = "Silver Drill - Distance Piston";
const string NAME_ROTOR = "Silver Drill - Advanced Rotor";
const string NAME_LCD = "Silver Drill - LCD";
/**/

/*** Drill A ***
private static List<string> NAME_DRILL_PISTONS = new List<String>() {
	"Drill A - Lower Drill Piston",
	"Drill A - Upper Drill Piston",
	"Drill A - Middle Drill Piston"};
private static List<string> NAME_LIFT_PISTONS = new List<String>() {
	"Drill A - Lift Piston" };
const string NAME_DISTANCE_PISTON = "Drill A - Distance Piston";
const string NAME_ROTOR = "Drill A - Rotor";
const string NAME_LCD = "Drill A - LCD";
/**/

/*** Drill B ***
private static List<string> NAME_DRILL_PISTONS = new List<String>() {
	"Drill B - Lower Drill Piston",
	"Drill B - Upper Drill Piston"};
private static List<string> NAME_LIFT_PISTONS = new List<String>() {
	"Drill B - Lift Piston - Upper",
	"Drill B - Lift Piston - Lower" };
const string NAME_DISTANCE_PISTON = "Drill B - Distance Piston";
const string NAME_ROTOR = "Drill B - Rotor";
const string NAME_LCD = "Drill B - LCD";
/**/

//*** Drill C ***
private static List<string> NAME_DRILL_PISTONS = new List<String>() {
	"Drill C - Drill Piston - 1st",
	"Drill C - Drill Piston - 2nd",
	"Drill C - Drill Piston - 3rd" };
private static List<string> NAME_LIFT_PISTONS = new List<String>() {
	"Drill C - Lift Piston - Upper",
	"Drill C - Lift Piston - Middle",
	"Drill C - Lift Piston - Lower" };
const string NAME_DISTANCE_PISTON = "Drill C - Distance Piston";
const string NAME_ROTOR = "Drill C - Rotor";
const string NAME_LCD = "Drill C - LCD";
/**/

/*** Iron Drill (BOD) ***
private static List<string> NAME_DRILL_PISTONS = new List<String>() {
	"Iron Drill - 1st Drill Piston",
	"Iron Drill - 2nd Drill Piston",
	"Iron Drill - 3rd Drill Piston"};
private static List<string> NAME_LIFT_PISTONS = new List<String>() {
	"Iron Drill - Upper Lift Piston",
	"Iron Drill - Lower Lift Piston" };
const string NAME_DISTANCE_PISTON = "Iron Drill - Distance Piston";
const string NAME_ROTOR = "Iron Drill - Advanced Rotor";
/**/


int motorSwing = 0; // How many times the rotor's direction has been reversed at the current distance/angle settings

// List of pistons used to adjust height of the drill. Does NOT include the distance piston.
List<IMyPistonBase> drillPistons = new List<IMyPistonBase>();
List<IMyPistonBase> liftPistons = new List<IMyPistonBase>();

// Advanced Rotor at the base of the rig
IMyMotorAdvancedStator rotor;

// LCD screen to use for output (nullable)
IMyTextSurface lcd = null;
StringBuilder screenText = new StringBuilder();

// Piston at the top of the drill which determines outward distance
IMyPistonBase distancePiston;

// Flag indicating we are waiting for pistons to reset to their initial position.
// This is done to avoid damaging the drill arm.
bool inPistonRetract = false;

// bool initialSetup = true;
bool haltAndCatchFire = false;


// Note: this function is called each time the Programmable Block is turned on 
// or the code is edited/recompiled.
public Program()
{
	// This makes the program automatically run every 100 ticks.
	// For more see: https://github.com/malware-dev/MDK-SE/wiki/Continuous-Running-No-Timers-Needed
	Runtime.UpdateFrequency = UpdateFrequency.Update100;

	foreach (string pistonName in NAME_DRILL_PISTONS) {
		var piston = GridTerminalSystem.GetBlockWithName(pistonName) as IMyPistonBase;
		drillPistons.Add(piston);
	}
	foreach (string pistonName in NAME_LIFT_PISTONS) {
		var piston = GridTerminalSystem.GetBlockWithName(pistonName) as IMyPistonBase;
		liftPistons.Add(piston);
	}

	distancePiston = GridTerminalSystem.GetBlockWithName(NAME_DISTANCE_PISTON) as IMyPistonBase;
	rotor = GridTerminalSystem.GetBlockWithName(NAME_ROTOR) as IMyMotorAdvancedStator;

	lcd = GridTerminalSystem.GetBlockWithName(NAME_LCD) as IMyTextSurface;
	ClearLCD();

	if (! Load()) {
		resetPistons();
	}
}

public void Save() {
	string data = "";
	data += String.Format("{0:.##}", distancePiston.CurrentPosition) + DELIMITER_GROUP;

	foreach(IMyPistonBase piston in liftPistons) {
		data += String.Format("{0:.##}", piston.CurrentPosition) + DELIMITER_ITEM;
	}
	data += DELIMITER_GROUP;

	foreach(IMyPistonBase piston in drillPistons) {
		data += String.Format("{0:.##}", piston.CurrentPosition) + DELIMITER_ITEM;
	}

	Storage = data;
}


/** Attempts to load data from storage **/
public bool Load() {
	if (Storage == null || Storage.Length == 0) {
		Output("No data in Storage");
		return false;
	}

	Output("Storage: " + Storage);
	string[] storedData = Storage.Split(DELIMITER_GROUP);
	if (storedData.Length == 0) {
		Output("Invalid data in Storage");
		return false;
	}
	
	float value;
	if (! float.TryParse(storedData[0], out value)) {
		Output("Invalid data in Storage");
		return false;
	}
	distancePiston.MaxLimit = value;

	try {
		string[] liftPistonData = storedData[1].Split(DELIMITER_ITEM);
		for (int index=0; index < liftPistonData.Length - 1; index++) {
			liftPistons[index].MinLimit = float.Parse(liftPistonData[index]);
		}

		string[] drillPistonData = storedData[2].Split(DELIMITER_ITEM);
		for (int index=0; index < drillPistonData.Length - 1; index++) {
			drillPistons[index].MaxLimit = float.Parse(drillPistonData[index]);
		}
	} catch {
		Output("Invalid data in Storage");
		Storage = "";
		return false;
	}

	return true;
}

/**
	Retracts all drill pistons, extends all lift pistons, and sets both min and max values to that max/min.
	Also retracts the distance piston.
*/
public void resetPistons() {
	foreach (IMyPistonBase piston in drillPistons) {
		// Initial piston setup for drill pistons = retracted
		piston.MaxLimit = piston.LowestPosition;
		piston.MinLimit = piston.LowestPosition;
		piston.Velocity = -Math.Abs(piston.Velocity);
	}
	foreach (IMyPistonBase piston in liftPistons) {
		// Initial piston setup for lift pistons = extended
		piston.MaxLimit = piston.HighestPosition;
		piston.MinLimit = piston.HighestPosition;
		piston.Velocity = Math.Abs(piston.Velocity);
	}
	distancePiston.MaxLimit = distancePiston.LowestPosition;
	distancePiston.MinLimit = distancePiston.LowestPosition;
	distancePiston.Velocity = -Math.Abs(distancePiston.Velocity);

	inPistonRetract = true;
}

public void Main()
{
	// if (initialSetup) {
	// 	resetPistons();
	// 	initialSetup = false;
	// }

	if (haltAndCatchFire) {
		return;
	}

	ClearLCD();

	//*****************************************************
	// Handles swinging the rotor back and forth, extending
	// the distace piston once the swings are complete.
	//*****************************************************
	if (inPistonRetract) {
		if (allPistonsStopped()) {
			Output("Retraction complete. Resetting velocities.");
			reverseAllPistons();
			inPistonRetract = false;
		} else {
			Output("Waiting on piston retraction");
			return;
		}
	}

	if (rotorStopped()) {
		rotor.TargetVelocityRPM = -rotor.TargetVelocityRPM;
		Output("Reversed rotor");
		Output("Velocity set to:" + rotor.TargetVelocityRPM + " RPM");
		if (motorSwing < MAX_ROTOR_SWINGS) {
			motorSwing += 1;
			Output("Starting swing #" + motorSwing);
		} else {

			Output("Completed all swings (" + motorSwing + ")");
			motorSwing = 0;

			if (distancePiston.MaxLimit == distancePiston.HighestPosition) {

				Output("Retracting distance piston");
				distancePiston.MaxLimit = distancePiston.LowestPosition;

				if (! lengthenNextPiston()) {
					Output("No pistons left to extend. Reversing all pistons");
					inPistonRetract = true;
					reverseAllPistons();
				}

				Save();

				// Make sure the distance piston is retracting either way as it hit its limit.
				distancePiston.Velocity = -Math.Abs(distancePiston.Velocity);

			} else {
				distancePiston.MaxLimit = Math.Min(distancePiston.HighestPosition, distancePiston.MaxLimit + DISTANCE_DELTA);
				Output("Setting Distance Piston's MaxLimit to:" + distancePiston.MaxLimit);
				// Make sure the distance piston will extend
				distancePiston.Velocity = Math.Abs(distancePiston.Velocity);
			}
		}
	} else {
		// Output current status
		Output("In swing #" + motorSwing);
		Output(distancePiston.CustomName + " at " + distancePiston.CurrentPosition);
		foreach (IMyPistonBase piston in drillPistons) {
			Output(piston.CustomName + " at " + piston.CurrentPosition);
		}
		foreach (IMyPistonBase piston in liftPistons) {
			Output(piston.CustomName + " at " + piston.CurrentPosition);
		}

		Output("Storage: " + Storage);
	}
}

public bool isDrillPiston(IMyPistonBase piston) {
	return piston.CustomName.Contains(PREFIX_DRILL_PISTON);
}

public bool isLiftPiston(IMyPistonBase piston) {
	return piston.CustomName.Contains(PREFIX_LIFT_PISTON);
}


public bool rotorStopped() {

	Output("Rotor Angle @ " + rotor.Angle);
	Output("Range: " + rotor.LowerLimitRad + " -> " + rotor.UpperLimitRad);
	if (rotor.Angle <= rotor.LowerLimitRad ||
		rotor.Angle >= rotor.UpperLimitRad) {
		return true;
	}
	return false;
}

public void reverseAllPistons() {
	foreach (IMyPistonBase piston in liftPistons) {
		piston.Reverse();
	}
	foreach (IMyPistonBase piston in drillPistons) {
		piston.Reverse();
	}
	distancePiston.Reverse();
}

public bool allPistonsStopped() {
	foreach (IMyPistonBase piston in liftPistons) {
		if (piston.Status == PistonStatus.Retracting || piston.Status == PistonStatus.Extending) {
			return false;
		}
	}
	foreach (IMyPistonBase piston in drillPistons) {
		if (piston.Status == PistonStatus.Retracting || piston.Status == PistonStatus.Extending) {
			return false;
		}
	}	
	return true;
}

/**
	Extends one drill piston or retracts one lift piston by HEIGHT_DELTA, returning true to indicate success.
	If no piston can be extended/retracted (i.e. the drill is at its lowest position), returns false.
*/
public bool lengthenNextPiston() {
	foreach (IMyPistonBase piston in drillPistons) {
		if (piston.CurrentPosition != piston.HighestPosition) {
			piston.MaxLimit = Math.Min(piston.MaxLimit + HEIGHT_DELTA, piston.HighestPosition);
			piston.Velocity = Math.Abs(piston.Velocity);
			Output("Extending " + piston.CustomName + " to " + piston.MaxLimit);
			return true;
		} else {
			Output(piston.CustomName + " at max");
		}
	}
	foreach (IMyPistonBase piston in liftPistons) {
		if (piston.CurrentPosition != piston.LowestPosition) {
			piston.MinLimit = Math.Max(piston.MinLimit - HEIGHT_DELTA, piston.LowestPosition);
			piston.Velocity = -Math.Abs(piston.Velocity);
			Output("Retracting " + piston.CustomName + " to " + piston.MinLimit);
			return true;
		} else {
			Output(piston.CustomName + " at max");
		}
	}
	Output("Drill at lowest point. Returning false.");
	haltAndCatchFire = true;
	return false;
}

/** Displays status on LCD if one is named. 
	Always echos to Programmable Block console */
private void Output(string text) {
	Echo(text);

	screenText.Append(text + "\n");
	if (lcd != null) {
		lcd.WriteText(screenText);
	}

	// IMyTextSurface lcd;
	// IMyTextPanel textPanel = GetMyLcdPanel(...);
	// IMyTextSurface surface = (IMyTextSurface)textPanel;
	// surface0.ContentType = ContentType.TEXT_AND_IMAGE;
	// surface0.FontSize = 2;
	// surface0.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.CENTER;
	// surface0.WriteText("Hello World");
}
private void ClearLCD() {
	screenText = new StringBuilder();
}


/**

Important SDK information:

Pistons- https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.IMyPistonBase
	Status:
	* CurrentPosition
	* Status (PistonStatus enum): see https://github.com/malware-dev/MDK-SE/wiki/Sandbox.ModAPI.Ingame.PistonStatus

	Settings:
	* MinLimit - minimum position the piston can retract to
	* MaxLimit - maximum ...

	Hardcoded limits:
	* LowestPosition - lowest retraction position which can be set on the piston (always 0.0)
	* HighestPosition - highest extension position which can be set on the piston (10.0 for large grid, 2.0 for small grid)

All figures are in meters.


Rotors-
	Velocity:
	* TargetVelocityRPM - Gets or sets the desired velocity of the rotor in **RPM**
	Note that positive RPM means clockwise 

	Direction:
	* Angle - Gets the current angle of the rotor in **radians**.
	* LowerLimitRad - lower angle limit of the rotor in ***radians**. Set to float.MinValue for no limit.
	* LowerLimitDeg - lower angle limit of the rotor in **degrees**. Set to float.MinValue for no limit.
	* UpperLimitDeg - upper angle limit of the rotor in degrees. Set to float.MaxValue for no limit.

	State:
	* RotorLock - get/set the rotor lock
**/
/** DrillingRig.cs v. 0.2

	Drilling program for Space Engineers.
	Author: mkazin
	Source: https://github.com/mkazin/SpaceEngineers/

	Description: this program will drill an area extending away from a drill rig.
**/

// Number of times a rotor will swing in an arc across the current distance
// Has a linear relationship with rotor RPM.
// Do not lower below two, which allows for distance piston to retract.
const int MAX_ROTOR_SWINGS = 2;

// Amount to increse the distance piston after we finish our swings. I'm assuming meters.
const float DISTANCE_DELTA = 2.0f;

// Amount to raise drill pistons/lower lift pistons between swing sets.
const float HEIGHT_DELTA = 0.4f;

// Constants which differ per drill
const string PREFIX_DRILL_NAME = "Drill";
const string PREFIX_DRILL_PISTON = "Drill Piston";
const string PREFIX_LIFT_PISTON = "Lift Piston";

/*** Some pre-defined drilling rigs I've set up ***/

/*** Magnesium Mine ****
private static List<string> NAME_VERTICAL_PISTONS = new List<String>() {
	"Drill Piston - 1st",
	"Drill Piston - 2nd",
	"Drill Piston - 3rd",
	"Drill - Lift Piston - Lower",
	"Drill - Lift Piston - Upper" };
const string NAME_DISTANCE_PISTON = "Drill - Distance Piston";
const string NAME_ROTOR = "Drill - Advanced Rotor";
/**/

//*** Money Pit (gold & silver) ****
private static List<string> NAME_VERTICAL_PISTONS = new List<String>() {
	"Drill Piston - 1st",
	"Drill Piston - 2nd",
	"Drill Piston - 3rd",
	"Drill Piston - 4th",
	"Lift Piston - Lower",
	"Lift Piston - Upper" };
const string NAME_DISTANCE_PISTON = "Distance Piston";
const string NAME_ROTOR = "Drill Rotor";
/**/

/*** Silver Drill (HQ) ***
private static List<string> NAME_VERTICAL_PISTONS = new List<String>() {
	"Silver Drill - Lower Drill Piston",
	"Silver Drill - Upper Drill Piston",
	"Silver Drill - Lift Piston" };
const string NAME_DISTANCE_PISTON = "Silver Drill - Distance Piston";
const string NAME_ROTOR = "Silver Drill - Advanced Rotor";
/**/

/*** Drill A ***
private static List<string> NAME_VERTICAL_PISTONS = new List<String>() {
	"Drill A - Lower Drill Piston",
	"Drill A - Upper Drill Piston",
	"Drill A - Middle Drill Piston",
	"Drill A - Lift Piston" };
const string NAME_DISTANCE_PISTON = "Drill A - Distance Piston";
const string NAME_ROTOR = "Drill A - Rotor";
/**/

/*** Drill B ***
private static List<string> NAME_VERTICAL_PISTONS = new List<String>() {
	"Drill B - Lower Drill Piston",
	"Drill B - Upper Drill Piston",
	"Drill B - Lift Piston - Upper",
	"Drill B - Lift Piston - Lower" };
const string NAME_DISTANCE_PISTON = "Drill B - Distance Piston";
const string NAME_ROTOR = "Drill B - Rotor";
/**/


/*** Iron Drill (BOD) ***
private static List<string> NAME_VERTICAL_PISTONS = new List<String>() {
	"Iron Drill - 1st Drill Piston",
	"Iron Drill - 2nd Drill Piston",
	"Iron Drill - 3rd Drill Piston",
	"Iron Drill - Upper Lift Piston",
	"Iron Drill - Lower Lift Piston" };
const string NAME_DISTANCE_PISTON = "Iron Drill - Distance Piston";
const string NAME_ROTOR = "Iron Drill - Advanced Rotor";
/**/


int motorSwing = 0; // How many times the rotor's direction has been reversed at the current distance/angle settings

// List of pistons used to adjust height of the drill. Does NOT include the distance piston.
List<IMyPistonBase> pistons = new List<IMyPistonBase>();

IMyMotorAdvancedStator rotor;

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

	foreach (string pistonName in NAME_VERTICAL_PISTONS) {
		var piston = GridTerminalSystem.GetBlockWithName(pistonName) as IMyPistonBase;
		pistons.Add(piston);
	}

    distancePiston = GridTerminalSystem.GetBlockWithName(NAME_DISTANCE_PISTON) as IMyPistonBase;
	rotor = GridTerminalSystem.GetBlockWithName(NAME_ROTOR) as IMyMotorAdvancedStator;

	resetPistons();
	// initialSetup = true;
}

/**
	Retracts all drill pistons, extends all lift pistons, and sets both min and max values to that max/min.
	Also retracts the distance piston.
*/
public void resetPistons() {
	foreach (IMyPistonBase piston in pistons) {
		// Initial piston setup -retract all pistons
		if (isDrillPiston(piston)) {
			piston.MaxLimit = piston.LowestPosition;
			piston.MinLimit = piston.LowestPosition;
			piston.Velocity = -Math.Abs(piston.Velocity);
		} else if (isLiftPiston(piston)) {
			piston.MaxLimit = piston.HighestPosition;
			piston.MinLimit = piston.HighestPosition;
			piston.Velocity = Math.Abs(piston.Velocity);
		} else {
			Echo("Unsupported Piston: " + piston.CustomName);
		}
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

	//*****************************************************
	// Handles swinging the rotor back and forth, extending
	// the distace piston once the swings are complete.
	//*****************************************************
    if (inPistonRetract) {
    	if (allPistonsStopped()) {
    		Echo("Retraction complete. Resetting velocities.");
    		reverseAllPistons();
    		inPistonRetract = false;
	    } else {
	    	Echo("Waiting on piston retraction");
	    	return;
	    }
    }

    if (rotorStopped()) {
		rotor.TargetVelocityRPM = -rotor.TargetVelocityRPM;
	    Echo("Reversed rotor");
	    Echo("Velocity set to:" + rotor.TargetVelocityRPM + " RPM");
		if (motorSwing < MAX_ROTOR_SWINGS) {
			motorSwing += 1;
		    Echo("Starting swing #" + motorSwing);
		} else {

		    Echo("Completed all swings (" + motorSwing + ")");
			motorSwing = 0;

			if (distancePiston.MaxLimit == distancePiston.HighestPosition) {

				Echo("Retracting distance piston");
				distancePiston.MaxLimit = distancePiston.LowestPosition;

				if (! lengthenNextPiston()) {
					Echo("No pistons left to extend. Reversing all pistons");
					inPistonRetract = true;
					reverseAllPistons();
				}

				// Make sure the distance piston is retracting either way as it hit its limit.
				distancePiston.Velocity = -Math.Abs(distancePiston.Velocity);

			} else {
				distancePiston.MaxLimit = Math.Min(distancePiston.HighestPosition, distancePiston.MaxLimit + DISTANCE_DELTA);
			    Echo("Setting Distance Piston's MaxLimit to:" + distancePiston.MaxLimit);
				// Make sure the distance piston will extend
				distancePiston.Velocity = Math.Abs(distancePiston.Velocity);
			}
		}
    } else {
		// Output current status
		Echo("In swing #" + motorSwing);
		foreach (IMyPistonBase piston in pistons) {
			Echo(piston.CustomName + " at " + piston.CurrentPosition);
		}
		Echo(distancePiston.CustomName + " at " + distancePiston.CurrentPosition);
    }
}

public bool isDrillPiston(IMyPistonBase piston) {
	return piston.CustomName.Contains(PREFIX_DRILL_PISTON);
}

public bool isLiftPiston(IMyPistonBase piston) {
	return piston.CustomName.Contains(PREFIX_LIFT_PISTON);
}


public bool rotorStopped() {

	Echo("Rotor Angle @ " + rotor.Angle);
	Echo("Range: " + rotor.LowerLimitRad + " -> " + rotor.UpperLimitRad);
	if (rotor.Angle <= rotor.LowerLimitRad ||
		rotor.Angle >= rotor.UpperLimitRad) {
		return true;
	}
	return false;
}

public void reverseAllPistons() {
	foreach (IMyPistonBase piston in pistons) {
		piston.Reverse();
	}
	distancePiston.Reverse();
}

public bool allPistonsStopped() {
	foreach (IMyPistonBase piston in pistons) {
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
	foreach (IMyPistonBase piston in pistons) {
		float targetPosition = isDrillPiston(piston) ? piston.HighestPosition : piston.LowestPosition;
		if (piston.CurrentPosition != targetPosition) {
			if (isDrillPiston(piston)) {
				piston.MaxLimit = Math.Min(piston.MaxLimit + HEIGHT_DELTA, piston.HighestPosition);
				piston.Velocity = Math.Abs(piston.Velocity);
				Echo("Extending " + piston.CustomName + " to " + piston.MaxLimit);
			} else {
				piston.MinLimit = Math.Max(piston.MinLimit - HEIGHT_DELTA, piston.LowestPosition);
				piston.Velocity = -Math.Abs(piston.Velocity);
				Echo("Retracting " + piston.CustomName + " to " + piston.MinLimit);
			}
			return true;
		} else {
			Echo(piston.CustomName + " at max");
		}
	}
	Echo("Drill at lowest point. Returning false.");
	haltAndCatchFire = true;
	return false;
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
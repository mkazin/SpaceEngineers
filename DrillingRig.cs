/** DrillingRig.cs v. 0.1

	Drilling program for Space Engineers.
	Author: mkazin
	Source: https://github.com/mkazin/SpaceEngineers

	Description: this program will drill an area extending away from a drill rig.

	The drill rig is composed of five sections. In the build order:
	- An Advanced Rotor at the base, used to swing the drill in an arc.
	  (notes: make sure to use Advanced Rotor, as the regular rotor will not convey drilled materials.
	          also make sure to leave room under it to connect a conveyer.)
	- m * Lift Pistons, attached to the rotor, facing upward.
	- A Distance Piston, connected to the top Lift Piston via conveyor junction or curved conveyer.
	- n * Drill Pistons, similarly connected to the attackment of the Distance piston, facing downward.
	- A drill, attached to the lowest Drill Piston

	Except for the rotor, a demonstration of this type of rig can be seen in part #5 of Splitsie's tutorial at:
	https://www.youtube.com/watch?v=knRgN0WhzKg&list=PLfMGCUepUcNzLePdu3dZfMTLfWq1bclUK&index=5

	Q: What shape is the area we are going to be drilling?

	The surface shape being cut is defind as an "Annular Sector", being a sector of the ring between two 
	concentric cylinders. See this page for a diagram and area calculator: 
	https://www.aqua-calc.com/calculate/area-annulus

	We'll also be drilling to a depth, which means multiplying the area providedin that calculator by 
	height to attain the total volume being excavated (the volume formula of a cylinder using its height).

	TODO: test this volume and compare to the removed material dumped into a large container with no refinery (1 cubic meter = 1000 liters).

	In plainer terms, this means we are drilling out the area between two arcs-
	- one whose radius is the sum of a rotor's radius and the length of a retracted piston (TODO: calculate this)
	- the second being the above, plus the extension length of the distance piston (10m on large grid)
	And we are drilling down to a depth defined by the total extension length of the lift+drill pistons (m + n),
	minus the height of the rotor off the ground, and then adjusted by any unsmooth terrain.

	(NOTE: this isn't perfectly accurate, as the drill will be extracting material both outside the angle defined 
	by the rotor, as well as outside the outer radius and inside the inner radius.)

	(TODO: this would be a lot clearer with an image)

	Usage:
		* Modify constants in NAME_VERTICAL_PISTONS to the names of your blocks
		* Modify the PREFIX_ constants to match text in your blocks (you may need to rename your blocks)
		* Enter your modified code a programmable cube. 
		* Program your rotor with upper and lower angle bounds; move the rotor to within those bounds.
		* Turn on the programmable cube.

	Future Development:
		* Tune default values for swings, velocities (either hard-code, or perhaps dynamically using m & n).
		* Auto-detect drilling rigs? (easy when a single rig is on the current grid, might be able to use subgrids off a rotor)
		* Support multiple Distance Pistons? (Would such a construct even be stable?)
		* Output? How about calculating an ETA? Swing count, Piston status?

	Theoretically this program should support small grid drilling rigs, but this has not yet been tested.
**/


// Number of times a rotor will swing in an arc across the current distance
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

int motorSwing = 0; // How many times the rotor's direction has been reversed at the current distance/angle settings

// List of pistons used to adjust height of the drill. Does NOT include the distance piston.
List<IMyPistonBase> pistons = new List<IMyPistonBase>();

IMyMotorAdvancedStator rotor;
// IMyPistonBase drillLowerPiston;

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
		// Echo(piston.CustomName);
		// Initial piston setup -retract all pistons
		if (piston.CustomName.Contains(PREFIX_DRILL_PISTON)) {
			piston.MaxLimit = piston.LowestPosition;
			piston.MinLimit = piston.LowestPosition;
			piston.Velocity = -Math.Abs(piston.Velocity);
			// Echo(piston.CustomName);
			// Echo("MaxLimit set to " + piston.MaxLimit);
			// Echo("Velocity set to " + piston.Velocity);
		} else if (piston.CustomName.Contains(PREFIX_LIFT_PISTON)) {
			piston.MaxLimit = piston.HighestPosition;
			piston.MinLimit = piston.HighestPosition;
			piston.Velocity = Math.Abs(piston.Velocity);
			// Echo(piston.CustomName);
			// Echo("MinLimit set to " + piston.MinLimit);
			// Echo("Velocity set to " + piston.Velocity);			
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
    Echo("Rotor at " + rotor.LowerLimitRad + " radians");

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

				Echo("Pulling back distance piston and extending next piston length");
				distancePiston.MaxLimit = distancePiston.LowestPosition;

				if (! lengthenNextPiston()) {
					Echo("Reversing all pistons");
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
    	Echo("In swing #" + motorSwing);
    }

    // if (allPistonsStopped() && rotorStopped()) {
    // 	if (drillLowerPiston) {

    // 	}
    // }
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

// public bool allPistonsExtended() {
// 	return pistons.Where(p => p.Status != PistonStatus.Extended).Any();
// 	// return pistons.Where(p => p.Status != Extended).Select(x => x.Status).Count() == 0;
// }

// public bool allPistonsRetracted() {
// 	return pistons.Where(p => p.Status != PistonStatus.Retracted).Any();
// }

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
		float targetPosition = piston.Velocity > 0 ? piston.HighestPosition : piston.LowestPosition;
		Echo(piston.CustomName);
		Echo("CurrentPosition: " + piston.CurrentPosition);
		Echo("targetPosition: " + targetPosition);
		Echo("MaxLimit : " + piston.MaxLimit);
		Echo("MinLimit : " + piston.MinLimit);
		if (piston.CurrentPosition != targetPosition) {
			if (piston.Velocity > 0) {
				piston.MaxLimit = Math.Min(piston.MaxLimit + HEIGHT_DELTA, piston.HighestPosition);
				Echo("Lengthening Piston: " + piston.CustomName + " to max of " + piston.MaxLimit);
			} else {
				piston.MinLimit = Math.Max(piston.MinLimit - HEIGHT_DELTA, piston.LowestPosition);
				Echo("Shortening Piston: " + piston.CustomName + " to min of " + piston.MinLimit);
			}
			return true;
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
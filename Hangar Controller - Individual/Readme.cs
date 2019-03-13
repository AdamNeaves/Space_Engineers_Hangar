/*
 *   R e a d m e
 *   -----------
 * 
 *   CREATED BY THE MERVINATOR - 07 Sept 2018
 *   Control script for each hangar space.
 *   Requires 3 timers set up to loop the script while waiting for certain actions, like the sensor detecting the ship has docked
 *   
 *   single argument accepted, one of the following:
 *   DOCK:
 *   Activates docking procedures. Opens doors, lights stuff up, then activates timer to wait for the sensor to detect the ship.
 *   Called by the Main Controller after recieving a request for a dock from a ship.
 *   
 *   UNDOCK:
 *   Activates undocking procedures. Extends the piston, rotates the platform, and opens the doors. Activates the timer to wait for the ship to leave.
 *   Called by the Main Controller after a docked ship requests undocking.
 *   
 *   SENSOR CHECK:      DO NOT MANUALLY CALL THIS ARGUMENT
 *   Instructs the program to check the sensor for the arrival of the ship, as a part of the DOCK procedure.
 *   Once the sensor detects the ship, closes the big door, then activates the timer to check the vent pressure.
 *   Called by a timer activated at the end of the DOCK section. Do NOT manually call.
 *   
 *   VENT CHECK:        DO NOT MANUALLY CALL THIS ARGUMENT
 *   Checks the pressure of the room. Unlocks the doors and turns on the interior lights once the pressure is at 100%
 *   Called by a timer activated once the sensor detects the ship has docked.
 *   
 *   DEPARTURE CHECK:   DO NOT MANUALLY CALL THIS ARGUMENT
 *   Checks a sensor to check if the ship has left the hangar. Closes the hangar door, retracts the piston.
 *   Called by a timer activated by the UNDOCK command.
 * 
 */
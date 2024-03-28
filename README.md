# MekNikon

.NET Nikon SDK Wrapper, Intervalometer, and maybe other things. Currently supports driving multiple cameras (NikonConsoleDriver) from a single executable (NikonHub) via a rather primitive CLI-based script interface.

## Attributions

Nikon FFIs and reference C# code were sourced from the following:

* https://github.com/1TTT9/NikonCSWrapper
* https://sourceforge.net/projects/nikoncswrapper/

## Setup and Instructions

This repo does not contain any Nikon libraries or materials; you will need to source them from Nikon at the following URI:  
https://sdk.nikonimaging.com/apply/

My recommendation is to enroll in the SDK forms at Nikon, then download all of the versions that you need to obtain the interface libraries (.md3 files), and then whether or not you need it, download the newest SDK version from Nikon, which will usually match the most recent device release (Nikon Z8 or Nikon Zfc at the time of writing this).

Inside the binaries folder (x64) for each of the downloaded versions, copy the **TypeNNNN.md3** files and place a copy in each output folder for the *NikonConsoleDriver* project in this repo. This is likely to be *./NikonConsoleDriver/bin/Debug/net8.0* relative to your local repo root.

In addition to the **.md3** files, copy the contents of the newest obtained SDK package (see above note on Nikon Z8 / Zfc) into the aforementioned directory. At this time, the following files should be present:  
* NkdPTP.dll
* dnssd.dll
* NkRoyalmile.dll
* **.md3** files corresponding to all the Nikon device versions you wish to use.

If you are using cameras other than the following, you will need to make some modifications to this repo to map the devices to the models so that the code can recognize your cameras. Note that some older Nikon models may not be usable under x64 because the currently available libraries for those devices are 32-bit and target older versions of Windows.

## Basic Operation

### Read This First

Note that the Nikon SDK has some limitations that influence the design of making something that can control multiple cameras at once.  
* Only one of a given camera model can be connected at a time; you cannot have two Nikon Z7 cameras, but you could have a Nikon Z7 and Z7ii connected to the host machine and operable at the same time.
* Only one camera can be driven per process; so to support multiple cameras, each thing driving that camera needs to be isolated in its own process.

### Supported Cameras

The following cameras are currently recognized, because these are the cameras I currently own:  
* Nikon D500
* Nikon Z7
* Nikon Z7 II

### Console Driver

*NikonConsoleDriver* is a headless controller that can host a single camera/device.

### Hub

*NikonHub* is a headless controller that can connect to multiple cameras by using scripts that define parameters for continuous operation of those cameras.

## Scripting

### Foreword

Note that my current use case is multi-camera continuous operation for the solar eclipse, and what I need is not readily available in COTS form; feel free to fork this repo and do your own thing if you have a novel use-case that needs to be addressed. If you submit a pull-request I'll do my best to integrate it as utility and time permits.

With the aforementioned in mind, the scripts support the following features, the bare minimum to make an intervalometer suitable for eclipse capture:
* Connect to Camera (used by the hub to figure out how to launch a console driver instance)
* Disconnect from Camera (used internally by the hub and drivers to try and gracefully clean-up during shutdown)
* Set ISO (Sensitivity)
* Set Aperture
* Set Shutter Speed
* Capture (take a picture)
* Declare a loop that runs continuously or N times, this loop can contain any of the *set* commands or *capture*

### Connect

```connect d500``` - Connects to a camera, which identifies which **.md3** file is dynamically loaded to drive the camera through the Nikon MAID interface.

### Disconnect

```disconnect``` - Disconnects, though at current there are multiple ways the sessions could be lost.

### Set ISO (Sensitivity)

```set_iso 400``` - Sets the ISO to a given value, which must match an enumerable value off of the physical camera. Note that some esoteric extended values may not follow a guaranteed convention. For example, some cameras might have *LO.3* (low) and others might have *LO-3* as the value accepted by the camera. This also applies for the *HI* (high) range as well. These labeled values are relative to base ISO as well; so *LO.3* might be ISO 50 on some cameras, and might be ISO 64 on others.

### Set Aperture

```set_aperture f/4``` - Sets the aperture to a given value, which must match an enumerable value off of the physical camera and also should match a value that is driveable on the lens. Note that lenses that do not use the electronic interface cannot have their aperture driven this way and the user will want to set the aperture on-lens. Note that I have not tested how the Nikon SDK behaves when the AI tab can be driven as the source of aperture information.

### Set Shutter Speed

```set_shutter 1/100``` / ```set_shutter 1/1.3``` / ```set_shutter 4``` - Sets the shutter speed to a given value, which must match an enumerable (and capable) value off of the physical camera. Note that some values may only be usable if the given camera is in a particular readout / shutter operating mode; at current this is likely to only apply to the Nikon Z8 and Nikon Z9, but other competing cameras already have specific constraints for global shutter versus electronic shutter.

### Capture (Take a photo!)

```capture``` - This triggers the shutter, and the image is stored on the Camera if successful. Be advised that the reference projects used to make the projects contained in this repo use live-download and have working examples to download the captured image immediately from the camera. However, this repo does not have code to use this facility as it is slow and it comes with a timeliness expense that is not acceptable during the 2-4 minute window of shooting within a solar eclipse. Other modes of operation also exist where the image is not saved by the Camera and held only within the internal buffer; some cameras will run out of working RAM quickly in this scenario, so proceed with caution if you modify how *capture* works.

### Loop

```loop:``` / ```loop: 5``` - This will cause the remainder of the script file to be interpreted as a block of instructions to be repeated a number of times or indefinitely. Theoretically the parser should handle multiple sections, but this is currently untested.
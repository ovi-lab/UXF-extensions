# UXF Extensions
A template for [UXF](https://github.com/immersivecognition/unity-experiment-framework) projects that uses [experiment server](https://github.com/ahmed-shariff/experiment_server) with a calibration phase before any given block.

## Installation
Install as a git dependency (https://docs.unity3d.com/Manual/upm-ui-giturl.html)

or

Add it as an embedded package - Clone/copy/submodule this repo into the `Packages` directory of your unity project.

## Usage

A representation of the process can be seen in the following activity chart. Note that the `user-defined` lane are functionalities expected to be provided by the user:

![Activity chart](Docs~/activity_chart.png)

To use this, extend the [`ubc.ok.ovilab.uxf.extensions.BlockData`](Assets/Scripts/BlockData.cs) to have the data from the experiment server. Then use that as the generic type and implement the [`ubc.ok.ovilab.uxf.extensions.ExperimentManager`](Assets/Scripts/ExperimentManager.cs) abstract class.

In the Unity Scene, ensure the extended `ExperimentManager` class is setup with the server URL pointing to the correct endpoint, setup the button and UI elements.

See documentation on [`ubc.ok.ovilab.uxf.extensions.ExperimentManager`](Assets/Scripts/ExperimentManager.cs) for more details.

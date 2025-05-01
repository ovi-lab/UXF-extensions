# Sample experiment-server setup

This is an example configuration of [experiment-server](https://shariff-faleel.com/experiment_server/) to work with [UXF-extensions](https://github.com/ovi-lab/UXF-extensions). This sample works with the "Sample implemenation of ExperimentManager" sample in UXF-Extensions.

## Installation of experiment-server

The recommende approach is to set this up with poetry [Poetry](https://poetry.eustace.io/). From the directory with the `pyproject.toml` file, run the following:
```sh
 poetry install
```

Alternatively, you can install it using pip:
```sh
 pip install .
```

See [experiment server documentation for more information on installation](https://shariff-faleel.com/experiment_server/#installation)

## Configuration
The `Assets/study.toml` has a sample configuration that is written to work with the "Sample implemenation of ExperimentManager" sample in UXF-extensions. See [documentation](https://shariff-faleel.com/experiment_server/#configuration-of-an-experiment) of experiment-server for more information on configuration file of experiment-server.

## Running experiment-server

The above installation adds an `experiment-server` cli application which can be executed from a terminal.
If installed using poetry:
```sh
 poetry run experiment-server run -i 10 Assets/study.toml
```

If installed using pip:
```sh
 experiment-server run -i 10 Assets/study.toml
```

The `-i` parameter indicates the participant id. See [documentation](https://shariff-faleel.com/experiment_server/#loading-experiment-through-server) on experiment-server for more details on the web UI that can be used to manage the experiment-server session.

This server needs to be launched before playing in Unity editor or before starting the built application. 

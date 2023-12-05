# ControlSync

This app allows you to play local multiplayer games with your friends over the network. It uses WebRTC to stream the screen of the host computer, where the game is running, to the other peers. It also uses TCP/UDP to sync the input of the peer computers with the host computer, so that each peer can control their own gamepad on the host. This way, you can enjoy playing games that support local multiplayer with your friends online.

**This is still a work in progress!**

This app was inspired by my desire to play [PCSX2](https://pcsx2.net/) games over the network with my friends.

## Features
* Simple user interface
* Supports any game that can run on Windows and has a local multiplayer mode with multiple gamepads.
* Uses WebRTC for screen sharing.
* Uses TCP/UDP for input synchronization.
* Supports input remapping

## Requirements

* Windows 10 or higher
* A game that supports local multiplayer with multiple gamepads
* A stable network connection

## Installation
1. Download the latest release of the app.
2. Extract the zip file to a folder of your choice.
3. Run the ControlSync.exe file to launch the app.

## Usage
To use this app, you need to either host a game or join a game.

### Hosting a game
To host a game, follow these steps:

0. Launch the app and set up your input in the Mapping tab.
1. Go to the Host tab and click on the “Start server” button.
2. Go to the Client window and connect to the server with a specified username. (Note that the 1st client to connect will be the host)
3. Run the game you'd like to play.

To join a game, follow these steps:

0. Launch the app and set up your input in the Mapping tab.
1. Go to the Client window and connect to the server with a specified username.

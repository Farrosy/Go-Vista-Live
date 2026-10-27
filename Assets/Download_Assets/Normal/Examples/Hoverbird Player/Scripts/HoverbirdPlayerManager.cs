#if NORMCORE

using UnityEngine;

namespace Normal.Realtime.Examples {
    public class HoverbirdPlayerManager : MonoBehaviour {
        [SerializeField] private GameObject _camera = default;
        [SerializeField] private GameObject _prefab;

        private Realtime _realtime;
        private HoverbirdPlayer _localPlayer;
        private bool _gameStarted;

        private void Awake() {
            // Get the Realtime component on this game object
            _realtime = GetComponent<Realtime>();

            // Notify us when Realtime successfully connects to the room
            _realtime.didConnectToRoom += DidConnectToRoom;
            _realtime.didDisconnectFromRoom += DidDisconnectFromRoom;
        }

        private void OnDestroy() {
            if (_realtime != null)
                _realtime.didConnectToRoom -= DidConnectToRoom;
            if (_realtime != null)
                _realtime.didDisconnectFromRoom -= DidDisconnectFromRoom;
        }

        private void DidConnectToRoom(Realtime realtime) {
            // Connecting to the room only enters the waiting lobby. The player
            // avatar is created after this client presses Join Game.
            _localPlayer = null;
            _gameStarted = false;
            HoverbirdMobileControls.SetVisible(false);
        }

        private void DidDisconnectFromRoom(Realtime realtime) {
            _localPlayer = null;
            _gameStarted = false;
            HoverbirdMobileControls.SetVisible(false);
        }

        public void BeginGame() {
            if (_gameStarted || _realtime == null || !_realtime.connected)
                return;

            _gameStarted = true;
            HoverbirdMobileControls.SetVisible(true);
            SpawnLocalPlayer(_realtime);
        }

        private void SpawnLocalPlayer(Realtime realtime) {
            var options = new Realtime.InstantiateOptions {
                ownedByClient            = true,    // Make sure the RealtimeView on this prefab is owned by this client.
                preventOwnershipTakeover = true,    // Prevent other clients from calling RequestOwnership() on the root RealtimeView.
                useInstance              = realtime // Use the instance of Realtime that fired the didConnectToRoom event.
            };
            // Give each client a deterministic, separate spawn point. Without
            // this, every player starts at (0, 0, 0) and their characters overlap.
            int clientID = Mathf.Max(0, realtime.clientID);
            float angle = clientID * 2.39996323f; // Golden angle spreads larger rooms evenly.
            float radius = 3.0f + Mathf.Sqrt(clientID) * 0.5f;
            Vector3 spawnPosition = new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
            GameObject playerGameObject = Realtime.Instantiate(_prefab.name, spawnPosition, Quaternion.identity, options);
            playerGameObject.name = "Player " + (clientID + 1);

            // Get a reference to the player
            HoverbirdPlayer player = playerGameObject.GetComponent<HoverbirdPlayer>();
            _localPlayer = player;
            player.SetGameplayEnabled(true);

            // Use the same free-look orbit camera as DevScene, targeting this client's avatar.
            CameraOrbit cameraOrbit = _camera.GetComponent<CameraOrbit>();
            // if (cameraOrbit != null)
            //     cameraOrbit.SetTarget(player.transform);
        }

    }
}

#endif

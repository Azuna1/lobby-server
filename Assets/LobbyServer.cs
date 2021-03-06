using UnityEngine;
using uLobby;
using System.Collections;
using System.IO;

public class LobbyServer : SingletonMonoBehaviour<LobbyServer> {
	// Default chat channels
	public static LobbyChatChannel globalChannel = new LobbyChatChannel("Global");
	public static LobbyChatChannel announceChannel = new LobbyChatChannel("Announcement");
	
	// Settings
	public int maxConnections = 1024;
	public int listenPort = 1310;
	public int frameRate = 20;
	public string mailFeedbackReceiver;
	public string privateKeyPath;
	public string loginMessagePath;
	public string smtpConfigPath;
	
	private string loginMessage;
	
#region Initialization
	// --------------------------------------------------------------------------------
	// Initialization
	// --------------------------------------------------------------------------------
	
	// Start
	void Start() {
		// System report
		LogManager.System.GenerateReport();
		
		// Limit frame rate
		Application.targetFrameRate = frameRate;
		
		// Configure SMTP so we can send mails
		InitMail();
		
		// Register codecs for serialization
		GameDB.InitCodecs();
		
		// Create log view scripts
		CreateLogViewScripts();
		
		// Start lobby
		StartLobby();
	}
	
	// StartLobby
	void StartLobby() {
		// Configure lobby
		ConfigureLobby();
		
		// After lobby initialization
		Lobby.OnLobbyInitialized += () => {
			LogManager.General.Log("Successfully initialized lobby.");
			
			// Private key
			LogManager.General.Log("Reading private key file");
			Lobby.privateKey = new PrivateKey(File.ReadAllText(privateKeyPath));
			
			// Login message
			LogManager.General.Log("Reading login message file");
			loginMessage = File.ReadAllText(loginMessagePath);
			
			// Security
			LogManager.General.Log("Initializing security");
			Lobby.InitializeSecurity(true);
			
			// Authoritative account manager
			LogManager.General.Log("Setting up account manager");
			AccountManager.Master.isAuthoritative = true;
			
			// Add ourselves as listeners for when accounts log in or out
			AccountManager.OnAccountLoggedIn += OnAccountLoggedIn;
			AccountManager.OnAccountLoggedOut += OnAccountLoggedOut;
			AccountManager.OnAccountRegistered += OnAccountRegistered;
			
			// Try to free up some RAM
			LogManager.General.Log("Freeing up RAM");
			System.GC.Collect();
			Resources.UnloadUnusedAssets();
			
			// Delete unactivated accounts
			//StartCoroutine(LobbyAccountManager.DeleteAllUnactivatedAccounts());
			
			// Copy accounts
			//StartCoroutine(LobbyAccountManager.CopyULobbyAccounts());
			
			/*var accountId = "/4P6sK+o";
			var pw = "reitareset";
			
			LogManager.General.Log(pw);
			StartCoroutine(LobbyAccountManager.CreateAccount(
				accountId,
				"reita93@web.de",
				pw
			));*/
		};
		
		// Peer connections
		Lobby.OnPeerConnected += OnPeerConnected;
		Lobby.OnPeerDisconnected += OnPeerDisconnected;
		
		// Make this class listen to Lobby events
		Lobby.AddListener(this);
		
		// Initialize the lobby
		LogManager.General.Log("Initializing lobby on port " + listenPort + " with a maximum of " + maxConnections + " players.");
		
		Lobby.InitializeLobby(
			maxConnections,
			listenPort,
			new RiakStorageManager(),
			new RiakAccountManager(),
			new RiakFriendManager()
		);
	}
	
	// ConfigureLobby
	void ConfigureLobby() {
		// Set new values
		Lobby.config.timeoutDelay = 20f;
		Lobby.config.timeBetweenPings = 5f;
		Lobby.config.handshakeRetriesMaxCount = 5;
		Lobby.config.handshakeRetryDelay = 2.5f;
		
		// Log
		LogManager.System.Log("MTU: " + Lobby.config.maximumTransmissionUnit);
		LogManager.System.Log("Timeout delay: " + Lobby.config.timeoutDelay);
		LogManager.System.Log("Time between pings: " + Lobby.config.timeBetweenPings);
		LogManager.System.Log("Handshake max. retries: " + Lobby.config.handshakeRetriesMaxCount);
		LogManager.System.Log("Handshake retry delay: " + Lobby.config.handshakeRetryDelay);
	}
	
	// InitMail
	void InitMail() {
		if(!File.Exists(smtpConfigPath)) {
			LogManager.General.LogError("Could not find SMTP configuration file: " + smtpConfigPath);
			return;
		}
		
		// Read config file
		var smtpConfig = File.ReadAllText(smtpConfigPath).Split('\n');
		
		// Set account data
		Mail.smtpUser = smtpConfig[0].Trim();
		Mail.smtpPassword = smtpConfig[1].Trim();
	}
	
	// Creates a few scripts on the server to make log reading easier
	void CreateLogViewScripts() {
		string cat = "#!/bin/sh\ncat ";
		string tail = "#!/bin/sh\ntail -f ";
		
		File.WriteAllText("./tail-general.sh", tail + LogManager.General.filePath);
		File.WriteAllText("./tail-online.sh", tail + LogManager.Online.filePath);
		File.WriteAllText("./tail-db.sh", tail + LogManager.DB.filePath);
		File.WriteAllText("./tail-chat.sh", tail + LogManager.Chat.filePath);
		File.WriteAllText("./tail-spam.sh", tail + LogManager.Spam.filePath);
		
		File.WriteAllText("./cat-general.sh", cat + LogManager.General.filePath);
		File.WriteAllText("./cat-online.sh", cat + LogManager.Online.filePath);
		File.WriteAllText("./cat-db.sh", cat + LogManager.DB.filePath);
		File.WriteAllText("./cat-chat.sh", cat + LogManager.Chat.filePath);
		File.WriteAllText("./cat-spam.sh", cat + LogManager.Spam.filePath);
	}
#endregion
	
#region Callbacks
	// --------------------------------------------------------------------------------
	// Callbacks
	// --------------------------------------------------------------------------------
	
	// Peer connected
	void OnPeerConnected(LobbyPeer peer) {
		// Log it
		var peerOnlineMsg = "Peer connected: " + peer;
		
		LogManager.General.Log(peerOnlineMsg);
		LogManager.Online.Log(peerOnlineMsg);
		
		// Send current version number to peer
		Lobby.RPC("VersionNumber", peer, Version.instance.versionNumber);
		
		// Look up country by IP
		IPInfoServer.GetCountry(peer);
	}
	
	// Peer disconnected
	public void OnPeerDisconnected(LobbyPeer peer) {
		StartCoroutine(RemovePeer(peer));
	}
	
	// RemovePeer
	IEnumerator RemovePeer(LobbyPeer peer) {
		// Log him out
		if(AccountManager.Master.IsLoggedIn(peer)) {
			// Perform logout
			var req = AccountManager.Master.LogOut(peer);
			yield return req.WaitUntilDone();
		}
		
		// Remove the player from all lists
		LobbyPlayer player;
		
		if(LobbyPlayer.peerToLobbyPlayer.TryGetValue(peer, out player)) {
			// Just to be safe, in case OnAccountLoggedOut failed
			player.Remove();
			
			// Remove player from peer list
			LobbyPlayer.peerToLobbyPlayer.Remove(peer);
			
			// Log it
			var msg = string.Format(
				"Removed player '{0}'. (E-Mail: '{1}', AccID: '{2}')",
				player.name,
				player.account.name,
				player.accountId
			);
			
			LogManager.General.Log(msg);
			LogManager.Online.Log(msg);
		}
	}
	
	// Account registered
	void OnAccountRegistered(Account account) {
		// Save registration date in database
		LobbyGameDB.SetAccountRegistrationDate(
			account.id.value,
			System.DateTime.UtcNow
		);
	}
	
	// Account login
	void OnAccountLoggedIn(Account account) {
		// Save the reference in a dictionary
		var player = new LobbyPlayer(account);
		
		// Disconnected already?
		// This can happen if the database takes too much time to respond.
		if(player.disconnected) {
			LogManager.General.LogWarning("Peer disconnected already, interrupting login process for: " + player.peer);
			return;
		}
		
		LogManager.General.Log("Account '" + account.name + "' logged in.");
		
		// Set online status
		player.onlineStatus = OnlineStatus.Online;
		
		// Async: Retrieve the player information
		SendPublicAccountInfo(
			player.accountId,	// Account ID
			player				// Receiver
		);
		
		// Others
		SettingsDB.GetInputSettings(player);
		AccessLevelsDB.GetAccessLevel(player);
		FriendsDB.GetFriends(player);
		FriendsDB.GetFollowers(player);
		
		// Async: Set last login date
		LobbyGameDB.SetLastLoginDate(player, System.DateTime.UtcNow);
		
		// Register access to this account by this IP
		IPInfoDB.RegisterAccountAccess(player.ip, player.accountId);
		
		// Save country
		player.UpdateCountry();
	}
	
	// Account logout
	void OnAccountLoggedOut(Account account) {
		//LogManager.General.Log("'" + account.name + "' logged out.");
		
		LobbyPlayer player;
		if(LobbyPlayer.accountIdToLobbyPlayer.TryGetValue(account.id.value, out player)) {
			player.Remove();
			
			var msg = string.Format(
				"'{0}' logged out. (Peer: {1}, E-Mail: '{2}', AccID: '{3}')",
				player.name,
				player.peer,
				player.account.name,
				player.accountId
			);
			
			// Log it
			LogManager.General.Log(msg);
			LogManager.Online.Log(msg);
		} else {
			var msg = string.Format(
				"Unknown player logged out, RemovePlayer has already been called. (E-Mail: '{0}', AccID: '{1}')",
				account.name,
				account.id.value
			);
			
			// Log it
			LogManager.General.LogWarning(msg);
			LogManager.Online.LogWarning(msg);
		}
	}
	
	// Once we have the player name...
	public static void OnReceivePlayerName(LobbyPlayer player) {
		// Map account ID to player name
		GameDB.accountIdToName[player.accountId] = player.name;
		
		// Log it
		var msg = "'" + player.name + "' logged in. (Peer: " + player.peer + ", Acc: '" + player.account.name + "', AccID: '" + player.accountId + "')";
		
		LogManager.General.Log(msg);
		LogManager.Online.Log(msg);
	}
	
	// On application quit
	void OnApplicationQuit() {
		// Close all file handles
		LogManager.CloseAll();
	}
#endregion
	
#region Utility
	// --------------------------------------------------------------------------------
	// Utility
	// --------------------------------------------------------------------------------
	
	// Sends a system message
	public static void SendSystemMessage(LobbyPlayer player, string msg) {
		Lobby.RPC("Chat", player.peer, "System", "", msg);
	}
	
	// Sends data about the account to any player
	public static void SendPublicAccountInfo(string accountId, LobbyPlayer toPlayer) {
		var player = GetLobbyPlayer(accountId);
		
		// Name
		LobbyGameDB.GetPlayerName(accountId, data => {
			if(data == null) {
				if(player == toPlayer)
					Lobby.RPC("AskPlayerName", toPlayer.peer);
			} else {
				Lobby.RPC("ReceivePlayerName", toPlayer.peer, accountId, data);
				
				if(player == toPlayer) {
					if(string.IsNullOrEmpty(player.name)) {
						player.name = data;
						LobbyServer.OnReceivePlayerName(player);
					}
				}
			}
		});
		
		// Character customization
		CharacterCustomizationDB.GetCharacterCustomization(accountId, data => {
			if(data == null) {
				if(player == toPlayer)
					Lobby.RPC("CustomizeCharacter", toPlayer.peer, accountId);
			} else {
				if(player != null)
					player.custom = data;
				Lobby.RPC("ReceiveCharacterCustomization", toPlayer.peer, accountId, data);
			}
		});
		
		// Skill build
		SkillBuildsDB.GetSkillBuild(accountId, data => {
			if(data == null) {
				Lobby.RPC("ReceiveSkillBuild", toPlayer.peer, accountId, SkillBuild.GetStarterBuild());
			} else {
				Lobby.RPC("ReceiveSkillBuild", toPlayer.peer, accountId, data);
			}
		});
		
		// Stats
		LobbyGameDB.GetPlayerStats(accountId, data => {
			if(data == null)
				data = new PlayerStats();
			
			// Assign stats
			if(player != null)
				player.stats = data;
			
			// Send the stats to the player
			Lobby.RPC("ReceivePlayerStats", toPlayer.peer,
				accountId,
				Jboy.Json.WriteObject(data)
			);
		});
		
		// FFA Stats
		LobbyGameDB.GetPlayerFFAStats(accountId, data => {
			if(data == null)
				data = new PlayerStats();
			
			// Assign stats
			if(player != null)
				player.ffaStats = data;
			
			// Send the stats to the player
			Lobby.RPC("ReceivePlayerFFAStats", toPlayer.peer,
				accountId,
				Jboy.Json.WriteObject(data)
			);
		});
		
		// Character stats
		TraitsDB.GetCharacterStats(accountId, data => {
			if(data == null)
				data = new CharacterStats();
			
			if(player != null)
				player.charStats = data;
			
			Lobby.RPC("ReceiveCharacterStats", toPlayer.peer, accountId, data);
		});
		
		// Artifact inventory
		ArtifactsDB.GetArtifactInventory(accountId, data => {
			if(data == null)
				data = new ArtifactInventory();
			
			if(player != null)
				player.artifactInventory = data;
			
			Lobby.RPC("ReceiveArtifactInventory", toPlayer.peer, accountId, Jboy.Json.WriteObject(data));
		});
		
		// Artifact tree
		ArtifactsDB.GetArtifactTree(accountId, data => {
			if(data == null)
				data = ArtifactTree.GetStarterArtifactTree();
			
			if(player != null)
				player.artifactTree = data;
			
			Lobby.RPC("ReceiveArtifactTree", toPlayer.peer, accountId, Jboy.Json.WriteObject(data));
		});
		
		// Experience
		ExperienceDB.GetExperience(accountId, data => {
			uint exp = 0;
			
			if(data != null)
				exp = data.experience;
			
			Lobby.RPC("ReceiveExperience", toPlayer.peer, accountId, exp);
		});
		
		// Item inventory
		ItemInventoryDB.GetItemInventory(accountId, data => {
			if(data == null)
				data = new ItemInventory();
			
			if(player != null)
				player.itemInventory = data;
			
			Lobby.RPC("ReceiveItemInventory", toPlayer.peer, accountId, Jboy.Json.WriteObject(data));
		});
		
		// View profile
		Lobby.RPC("ViewProfile", toPlayer.peer, accountId);
	}
	
	// SendStaffInfo
	public static void SendStaffInfo(LobbyPlayer player) {
		LogManager.General.Log("Sending staff info to " + player.accessLevel + " '" + player.name + "' (" + player.account.name + ")...");
		
		// VIP and higher
		if(player.accessLevel >= AccessLevel.CommunityManager) {
			// Last logins
			LobbyGameDB.GetLastLogins(20, data => {
				if(data != null)
					Lobby.RPC("ReceiveLastLogins", player.peer, data, false);
				else
					LogManager.DB.LogError("Couldn't fetch last logins!");
			});
			
			// Last registrations
			LobbyGameDB.GetLastRegistrations(20, data => {
				if(data != null)
					Lobby.RPC("ReceiveLastRegistrations", player.peer, data, false);
				else
					LogManager.DB.LogError("Couldn't fetch last registrations!");
			});
		}
	}
	
	// Gets the lobby player by the supplied message info
	public static LobbyPlayer GetLobbyPlayer(LobbyMessageInfo info) {
		Account account = AccountManager.Master.GetLoggedInAccount(info.sender);
		return LobbyPlayer.accountIdToLobbyPlayer[account.id.value];
	}
	
	// Gets the lobby player by the account ID
	public static LobbyPlayer GetLobbyPlayer(string accountId) {
		LobbyPlayer player;
		
		if(!LobbyPlayer.accountIdToLobbyPlayer.TryGetValue(accountId, out player))
			return null;
		
		return player;
	}
#endregion
	
#region RPCs
	// --------------------------------------------------------------------------------
	// RPCs
	// --------------------------------------------------------------------------------
	
	[RPC]
	void Ready(LobbyMessageInfo info) {
		LobbyPlayer player = GetLobbyPlayer(info);
		
		LogManager.General.Log(string.Format("Player '{0}' is now ready after logging in, connecting him to town", player.name));
		
		// Return player to his location in the game world
		LocationsDB.GetLocation(player.accountId, (data) => {
			// If that's the first time logging in, set the location to the default map
			if(data == null)
				data = new PlayerLocation(MapManager.startingMap, ServerType.World);
			
			// This will connect the player to a new or existing server
			// using the location data we just received.
			player.location = data;
		});
		
		// Add player to chat channels
		LobbyServer.globalChannel.AddPlayer(player);
		LobbyServer.announceChannel.AddPlayer(player);
		
		// Login message
		SendSystemMessage(player, loginMessage);
	}
	
	[RPC]
	void RequestGameServerInfo(LobbyMessageInfo info) {
		LobbyPlayer player = GetLobbyPlayer(info);
		
		Lobby.RPC("ReceiveServerType", player.peer, player.gameInstance.serverType);
		Lobby.RPC("LoadMap", player.peer, player.gameInstance.mapName);
	}
	
	[RPC]
	void GameServerReady(string instanceIdNumber, LobbyMessageInfo info) {
		LogManager.General.Log("[" + info.sender.endpoint + "] Game server ID " + instanceIdNumber + " is ready");
		
		var instance = LobbyInstanceManager.GetGameInstanceByID(instanceIdNumber);
		if(instance == null)
			LogManager.General.LogError("Could not identify server with ID " + instanceIdNumber);
		
		LogManager.General.Log("Playing on ID " + instanceIdNumber + " has started: " + instance);
		
		// TODO: Find instance by ID
		// TODO: instance.StartPlaying();
	}
	
	[RPC]
	IEnumerator PlayerNameChange(string newName, LobbyMessageInfo info) {
		// Prettify to be safe
		newName = newName.PrettifyPlayerName();
		
		// Validate data
		if(!Validator.playerName.IsMatch(newName)) {
			LogManager.General.LogError("Player name is not valid: '" + newName + "'");
			yield break;
		}
		
		// Check if name exists already
		yield return LobbyGameDB.GetAccountIdByPlayerName(newName, data => {
			if(data != null) {
				Lobby.RPC("PlayerNameAlreadyExists", info.sender, newName);
			} else {
				// Get the account
				LobbyPlayer player = GetLobbyPlayer(info);
				
				// Change name
				LogManager.General.Log("Account " + player.accountId + " has requested to change its player name to '" + newName + "'");
				LobbyGameDB.SetPlayerName(player, newName);
			}
		});
	}
	
	[RPC]
	IEnumerator PlayerNameExists(string newName, LobbyMessageInfo info) {
		yield return LobbyGameDB.GetAccountIdByPlayerName(newName, data => {
			if(data != null) {
				Lobby.RPC("PlayerNameAlreadyExists", info.sender, newName);
			} else {
				Lobby.RPC("PlayerNameFree", info.sender, newName);
			}
		});
	}
	
	[RPC]
	IEnumerator ViewProfile(string playerName, LobbyMessageInfo info) {
		LobbyPlayer playerRequesting = GetLobbyPlayer(info);
		
		yield return LobbyGameDB.GetAccountIdByPlayerName(playerName, data => {
			if(data != null) {
				SendPublicAccountInfo(
					data,				// Account ID
					playerRequesting	// Receiver
				);
			} else {
				Lobby.RPC("ViewProfileError", info.sender, playerName);
			}
		});
	}
	
	[RPC]
	void RequestPlayerName(string accountId, LobbyMessageInfo info) {
		// Cached?
		string playerName;
		if(GameDB.accountIdToName.TryGetValue(accountId, out playerName)) {
			Lobby.RPC("ReceivePlayerName", info.sender, accountId, playerName);
			return;
		}
		
		// Query name
		LobbyGameDB.GetPlayerName(accountId, data => {
			if(data != null) {
				Lobby.RPC("ReceivePlayerName", info.sender, accountId, data);
				GameDB.accountIdToName[accountId] = data;
			}
		});
	}

	[RPC]
	void RequestPlayerEmail(string accountId, LobbyMessageInfo info) {
		LogManager.General.Log("Requested mail for: " + accountId);

		// Query email
		LobbyGameDB.GetEmail(accountId, data => {
			if(data != null) {
				Lobby.RPC("ReceivePlayerEmail", info.sender, accountId, data);
			}
		});
	}
	
	[RPC]
	void MailFeedback(string text, LobbyMessageInfo info) {
		LobbyPlayer player = GetLobbyPlayer(info);
		LogManager.General.Log("Sending feedback mail from '" + player.name + "'...");
		
		Mail.Send(
			mailFeedbackReceiver,
			"Feedback from '" + player.name + "'",
			"E-Mail: " + player.account.name + "\n\n" + text,
			player.account.name
		);
	}
	
	[RPC]
	void StaffInfoRequest(LobbyMessageInfo info) {
		LobbyPlayer player = GetLobbyPlayer(info);
		
		// Access level check
		if(player.accessLevel == AccessLevel.Player) {
			LogManager.General.LogWarning("Player '" + player.name + "' attempted to get staff information!");
			return;
		}
		
		SendStaffInfo(player);
	}
	
	[RPC]
	void ActivatePortal(string mapName, string targetMapName, ServerType serverType, LobbyMessageInfo info) {
		LobbyPlayer player = GetLobbyPlayer(info);
		
		// Log
		LogManager.General.Log(string.Format("{0} went through a portal from map {1} to map {2}", player, mapName, targetMapName));
		
		// Save target portal so the game server can look it up
		PortalDB.SetPortal(
			player.accountId,
			new PortalInfo(mapName),
			data => {
				// Connect
				if(data != null)
					player.location = new PlayerLocation(targetMapName, serverType);
			}
		);
	}
#endregion
	
	/*// Start town servers
	protected void StartTownServers() {
		foreach(var mapName in MapManager.towns) {
			new LobbyTown(mapName).Register();
		}
	}
	
	// Stop town servers
	protected IEnumerator StopTownServers() {
		foreach(var townInstance in LobbyTown.running) {
			//uZone.InstanceManager.StopInstance(townInstance.instance.id);
			var stopRequest = townInstance.instance.Stop();
			
			yield return stopRequest.WaitUntilDone();
			
			if(stopRequest.isSuccessful) {
				LogManager.General.Log("Successfully terminated instance " + townInstance);
			}
		}
	}*/
}

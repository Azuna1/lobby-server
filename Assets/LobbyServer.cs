using UnityEngine;
using uLobby;
using System.Collections;
using System.Collections.Generic;

public class LobbyServer : MonoBehaviour {
	public static LobbyServer instance;
	
	// Default chat channels
	public static LobbyChatChannel globalChannel = new LobbyChatChannel("Global");
	public static LobbyChatChannel announceChannel = new LobbyChatChannel("Announcement");
	
	// Database components
	public static AccessLevelsDB accessLevelsDB;
	public static TraitsDB traitsDB;
	public static FriendsDB friendsDB;
	public static ItemInventoryDB itemInventoryDB;
	public static SettingsDB settingsDB;
	public static IPInfoDB ipInfoDB;
	
	// Settings
	public int maxConnections = 1024;
	public int listenPort = 1310;
	public string databaseHost;
	public int databasePort = 8087;
	public int frameRate = 20;
	
	private Version serverVersion;
	
	// Awake
	void Awake() {
		LobbyServer.instance = this;
		
		// System report
		LogManager.System.GenerateReport();
		
		// Register codecs for serialization
		GameDB.InitCodecs();
		
		// Limit frame rate
		Application.targetFrameRate = frameRate;
	}
	
	// Start
	void Start() {
		// Create log view scripts
		CreateLogViewScripts();
		
		// Get DB components
		accessLevelsDB = GetComponent<AccessLevelsDB>();
		traitsDB = GetComponent<TraitsDB>();
		itemInventoryDB = GetComponent<ItemInventoryDB>();
		settingsDB = GetComponent<SettingsDB>();
		ipInfoDB = GetComponent<IPInfoDB>();
		friendsDB = GetComponent<FriendsDB>();
		
		// Version number
		serverVersion = GetComponent<Version>();
		
		// Event handlers
		Lobby.OnLobbyInitialized += OnLobbyInitialized;
		Lobby.OnSecurityInitialized += OnSecurityInitialized;
		
		// Make this class listen to Lobby events
		Lobby.AddListener(this);
		
		// Configure lobby
		ConfigureLobby();
		
		// Initialize the lobby
		LogManager.General.Log("Initializing lobby on port " + listenPort + " with a maximum of " + maxConnections + " players.");
		Lobby.InitializeLobby(maxConnections, listenPort, databaseHost, databasePort);
	}
	
	// ConfigureLobby
	void ConfigureLobby() {
		// Set new values
		Lobby.config.timeoutDelay = 15f;
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
	
	// Creates a few scripts on the server to make log reading easier
	void CreateLogViewScripts() {
		string cat = "#!/bin/sh\ncat ";
		string tail = "#!/bin/sh\ntail -f ";
		
		System.IO.File.WriteAllText("./tail-general.sh", tail + LogManager.General.filePath);
		System.IO.File.WriteAllText("./tail-online.sh", tail + LogManager.Online.filePath);
		System.IO.File.WriteAllText("./tail-db.sh", tail + LogManager.DB.filePath);
		System.IO.File.WriteAllText("./tail-chat.sh", tail + LogManager.Chat.filePath);
		System.IO.File.WriteAllText("./tail-spam.sh", tail + LogManager.Spam.filePath);
		
		System.IO.File.WriteAllText("./cat-general.sh", cat + LogManager.General.filePath);
		System.IO.File.WriteAllText("./cat-online.sh", cat + LogManager.Online.filePath);
		System.IO.File.WriteAllText("./cat-db.sh", cat + LogManager.DB.filePath);
		System.IO.File.WriteAllText("./cat-chat.sh", cat + LogManager.Chat.filePath);
		System.IO.File.WriteAllText("./cat-spam.sh", cat + LogManager.Spam.filePath);
	}
	
	// Sends a system message
	public static void SendSystemMsg(LobbyPlayer player, string msg) {
		Lobby.RPC("Chat", player.peer, "System", "", msg);
	}
	
	// Sends data about the account to any player
	void SendPublicAccountInfo(string accountId, LobbyPlayer toPlayer) {
		LobbyPlayer player = GetLobbyPlayer(accountId);
		
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
		traitsDB.GetCharacterStats(accountId, data => {
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
		
		// Item inventory
		itemInventoryDB.GetItemInventory(accountId, data => {
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
	public void SendStaffInfo(LobbyPlayer player) {
		LogManager.General.Log("Sending staff info to " + player.accessLevel.ToString() + " '" + player.name + "' (" + player.account.name + ")...");
		
		// VIP and higher
		if(player.accessLevel >= AccessLevel.VIP) {
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
	
	// Start town servers
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
	
#region Callbacks
	// --------------------------------------------------------------------------------
	// Callbacks
	// --------------------------------------------------------------------------------
	
	// Peer connected
	void OnPeerConnected(LobbyPeer peer) {
		var peerOnlineMsg = "Peer connected: " + peer;
		
		LogManager.General.Log(peerOnlineMsg);
		LogManager.Online.Log(peerOnlineMsg);
		
		Lobby.RPC("VersionNumber", peer, serverVersion.versionNumber);
		
		// Look up country by IP
		StartCoroutine(IPInfoServer.GetCountryByIP(peer.endpoint.Address.ToString()));
	}
	
	// Peer disconnected
	public void OnPeerDisconnected(LobbyPeer peer) {
		// Log him out
		if(AccountManager.Master.IsLoggedIn(peer)) {
			var msg = "Peer disconnected, logging him out: " + peer;
			
			LogManager.General.Log(msg);
			LogManager.Online.Log(msg);
			
			// Perform logout
			var req = AccountManager.Master.LogOut(peer);
			req.WaitUntilDone();
		} else {
			var msg = "Peer disconnected (not logged in anymore): " + peer;
			
			LogManager.General.Log(msg);
			LogManager.Online.Log(msg);
		}
		
		// Clean up
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
		if(player.peer.type == LobbyPeerType.Disconnected) {
			LogManager.General.LogWarning("Peer disconnected already, interrupting login process for: " + player.peer);
			return;
		}
		
		LogManager.General.Log("Account '" + account.name + "' logged in.");
		
		// Set online status
		player.onlineStatus = OnlineStatus.Online;
		
		// Async: Retrieve the player information
		SendPublicAccountInfo(player.accountId, player);
		
		// Others
		settingsDB.GetInputSettings(player);
		accessLevelsDB.GetAccessLevel(player);
		friendsDB.GetFriends(player);
		friendsDB.GetFollowers(player);
		
		// Async: Set last login date
		LobbyGameDB.SetLastLoginDate(player, System.DateTime.UtcNow);
		
		// Save IP
		string ip = player.peer.endpoint.Address.ToString();
		string accountId = player.accountId;
		
		// Get and set account list for that IP
		ipInfoDB.GetAccounts(
			ip,
			data => {
				List<string> accounts;
				
				if(data == null) {
					accounts = new List<string>();
				} else {
					accounts = new List<string>(data);
				}
				
				// Save new account id
				if(accounts.IndexOf(accountId) == -1)
					accounts.Add(accountId);
				
				ipInfoDB.SetAccounts(
					ip,
					accounts.ToArray(),
					ignore => {}
				);
			}
		);
		
		// Save country
		if(IPInfoServer.ipToCountry.ContainsKey(ip)) {
			ipInfoDB.SetCountry(
				player.accountId,
				IPInfoServer.ipToCountry[ip],
				data => {
					if(data != null) {
						IPInfoServer.accountIdToCountry[player.accountId] = data;
					}
				}
			);
		}
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
	
	// Lobby initialized
	void OnLobbyInitialized() {
		LogManager.General.Log("Successfully initialized lobby.");
		
		// Private key
		Lobby.privateKey = new uLobby.PrivateKey(
@"<RSAKeyValue>
<Modulus>td076m4fBadO7bRuEkoOaeaQT+TTqMVEWOEXbUBRXZwf1uR0KE8A/BbOWNripW1eZinvsC+skgVT/G8mrhYTWVl0TrUuyOV6rpmgl5PnoeLneQDEfrGwFUR4k4ijDcSlNpUnfL3bBbUaI5XjPtXD+2Za2dRXT3GDMrePM/QO8xE=</Modulus>
<Exponent>EQ==</Exponent>
<P>yKHtauTiTeBpUlHDHIya+3p0/YSWrUTJGgsx8tPW7hT4mq9DySSvGd1SzWLBdZ1BWpIA0l2jmK3ptLJjGIc3pw==</P>
<Q>6A1hp1ZZ/0o7dULdFXvRJvRCTX5rQaUFYWFn7uRvxneMSKA/6SNLzxr91N2tILQx4vbXSOoO0w7DyS64qU3Whw==</Q>
<DP>OwJzAVJgrX49GDYqU7DiSfbXHWM7YCNKNNYdv+Pz66vQpfdQLBnZJbmQ0v7tmxAiR9CW1HXk0o2A+OksNGQBTw==</DP>
<DQ>sXOlB35E0kfTHW9dxSJyw299/wZSBQW40f8xXFRVeaa2keP0ozkb2pwrhKmEZE2Pj3F3c/5HklaVt/aNNix23w==</DQ>
<InverseQ>PH6IVe1Ccx5NP8o+NrNCyGxXXIjRGlbqX7lN5R4TysMCbLnYdaqApNv518NeO57f3zK5ZyeZPk7gHMe/i1U4Aw==</InverseQ>
<D>IBf7g7kUiIbvz5hPqN/kbQqR7/s0aRPAxGP1E0eV41fJYihQu9G04TEzeReRaHy2TkOixL0edB8O0jG7iCIDadJ9Hg2ygjj/EMq20U2BvjEGQE1AQzFuSLoRLA5lqqh81BBTShEZ8ti6rMGVM872GAc0HmvzskxQNaDEXp9zoN0=</D>
</RSAKeyValue>");
		
		// Security
		Lobby.InitializeSecurity(true);
		
		// Authoritative
		AccountManager.Master.isAuthoritative = true;
		
		// Add ourselves as listeners for when accounts log in or out.
		AccountManager.OnAccountLoggedIn += OnAccountLoggedIn;
		AccountManager.OnAccountLoggedOut += OnAccountLoggedOut;
		AccountManager.OnAccountRegistered += OnAccountRegistered;
		
		// Lobby connect
		Lobby.OnPeerConnected += OnPeerConnected;
		Lobby.OnPeerDisconnected += OnPeerDisconnected;
		
		// Try to free up some RAM
		System.GC.Collect();
		Resources.UnloadUnusedAssets();
	}
	
	// OnSecurityInitialized
	void OnSecurityInitialized(LobbyPeer peer) {
		//Debug.Log ("Initialized security for peer " + peer);
	}
	
	// Once we have the player name, let him join the channel
	public static void OnReceivePlayerName(LobbyPlayer player) {
		GameDB.accountIdToName[player.accountId] = player.name;
		
		var msg = "'" + player.name + "' logged in. (Peer: " + player.peer + ", Acc: '" + player.account.name + "', AccID: '" + player.accountId + "')";
		
		LogManager.General.Log(msg);
		LogManager.Online.Log(msg);
	}
	
	// Account register failed
	/*void OnRegisterFailed(string accountName, AccountError error) {
		// Bug in uLobby: We need to call this explicitly on the client
		Lobby.RPC("_RPCOnRegisterAccountFailed", info.sender, registerReq.result);
	}*/
	
	// On application quit
	void OnApplicationQuit() {
		LogManager.CloseAll();
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
		
		// Connect to town
		player.ReturnToWorld();
		
		// Chat channels
		LobbyServer.globalChannel.AddPlayer(player);
		LobbyServer.announceChannel.AddPlayer(player);
		
		SendSystemMsg(player, "All alpha testers will receive unique rewards at the end of the Open Alpha.");
		SendSystemMsg(player, "Thanks for testing this game.");
		SendSystemMsg(player, "Type //practice if you'd like to practice.");
	}
	
	[RPC]
	IEnumerator PlayerNameChange(string newName, LobbyMessageInfo info) {
		// Prettify to be safe
		newName = newName.PrettifyPlayerName();
		
		// Validate data
		if(!Validator.playerName.IsMatch(newName))
			yield break;
		
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
	IEnumerator ChangePassword(string newPassword, LobbyMessageInfo info) {
		// Get the account
		LobbyPlayer player = GetLobbyPlayer(info);
		
		// Change name
		LogManager.General.Log("Player '" + player.name + "' has requested to change its password.");
		yield return LobbyGameDB.SetPassword(player, newPassword);
	}
	
	[RPC]
	IEnumerator ViewProfile(string playerName, LobbyMessageInfo info) {
		LobbyPlayer playerRequesting = GetLobbyPlayer(info);
		
		yield return LobbyGameDB.GetAccountIdByPlayerName(playerName, data => {
			if(data != null) {
				SendPublicAccountInfo(data, playerRequesting);
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
	void MailFeedback(string text, LobbyMessageInfo info) {
		LobbyPlayer player = GetLobbyPlayer(info);
		LogManager.General.Log("Sending feedback mail from '" + player.name + "'...");
		
		Mail.Send(
			"e.urbach@gmail.com",
			"Feedback from '" + player.name + "'",
			"E-Mail: " + player.account.name + "\n\n" + text,
			player.account.name
		);
	}
	
	[RPC]
	void StaffInfoRequest(LobbyMessageInfo info) {
		LobbyPlayer player = GetLobbyPlayer(info);
		
		if(player.accessLevel == AccessLevel.Player) {
			LogManager.General.LogWarning("Player '" + player.name + "' attempted to get staff information!");
			return;
		}
		
		SendStaffInfo(player);
	}
#endregion
}

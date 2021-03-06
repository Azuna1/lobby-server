using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using uLobby;

public class RankingsServer : SingletonMonoBehaviour<RankingsServer> {
	// Players per page
	private static uint maxPlayerCount = 10;
	
	// Start
	void Start() {
		// Init ranking lists
		GameDB.InitRankingLists();
		
		// Update ranking list cache
		StartRankingListCacheUpdate();
		
		// Make this class listen to lobby events
		Lobby.AddListener(this);
	}
	
	// Updates the cached ranking list
	public void StartRankingListCacheUpdate(LobbyMatch match = null) {
		byte subject = (byte)RankingSubject.Player;
		
		for(byte page = 0; page < GameDB.numRankingPages; page++) {
			byte pageSaved = page;
			
			StartCoroutine(RankingsDB.GetTopRanks(
				subject,
				page,
				maxPlayerCount,
				data => {
					if(match != null) {
						foreach(var team in match.teams) {
							foreach(var player in team) {
								if(player.peer.type != LobbyPeerType.Disconnected)
									Lobby.RPC("ReceiveRankingList", player.peer, subject, pageSaved, data, false);
							}
						}
					}
				}
			));
		}
	}
	
	// --------------------------------------------------------------------------------
	// RPCs
	// --------------------------------------------------------------------------------
	
	[RPC]
	void RankingListRequest(byte subject, byte page, LobbyMessageInfo info) {
		// Cache
		var cached = GameDB.rankingLists[subject][page];
		if(cached != null) {
			Lobby.RPC("ReceiveRankingList", info.sender, subject, page, cached, false);
			return;
		}
		
		// RPC target
		var peer = info.sender;
		
		//LogManager.General.Log("Retrieving top " + maxPlayerCount + " ranks");
		StartCoroutine(RankingsDB.GetTopRanks(
			subject,
			page,
			maxPlayerCount,
			data => {
				Lobby.RPC("ReceiveRankingList", peer, subject, page, data, false);
			}
		));
	}
}

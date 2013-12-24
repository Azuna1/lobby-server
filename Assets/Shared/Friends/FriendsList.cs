﻿using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class FriendsList : JsonSerializable<FriendsList> {
	// This should keep user-defined order, so don't make it a HashSet
	public List<FriendsGroup> groups;

	// Constructor
	public FriendsList() {
		groups = new List<FriendsGroup>();
		groups.Add(new FriendsGroup("General"));
	}
	
	// AddGroup
	public void AddGroup(string groupName) {
		groups.Add(new FriendsGroup(groupName));
	}
	
	// RemoveGroup
	public void RemoveGroup(string groupName) {
		groups.RemoveAll(grp => grp.name == groupName);
	}
	
	// GetGroup
	public FriendsGroup GetGroupByName(string groupName) {
		return groups.Find(grp => grp.name == groupName);
	}
	
	// CanAdd
	public bool CanAdd(string accountId) {
		foreach(var grp in groups) {
			foreach(var friend in grp.friends) {
				if(friend.accountId == accountId)
					return false;
			}
		}
		
		return true;
	}
}
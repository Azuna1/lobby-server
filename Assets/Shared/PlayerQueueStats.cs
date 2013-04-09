using UnityEngine;
using System.Collections;

[System.Serializable]
public class PlayerQueueStats {
	public int ranking;
	public int rankingOffset;
	
	public int kills;
	public int deaths;
	public int assists;
	
	public int wins;
	public int losses;
	public int leaves;
	
	public int hits;
	public int hitsTaken;
	
	public int blocks;
	public int blocksTaken;
	
	public int lifeDrain;
	public int lifeDrainTaken;
	
	public long damage;
	public long damageTaken;
	
	public long cc;
	public long ccTaken;
	
	public int runeDetonations;
	public int runeDetonationsTaken;
	
	public int runeDetonationsLevel;
	public int runeDetonationsLevelTaken;
	
	public int topScorerOwnTeam;
	public int topScorerAllTeams;
	
	public double secondsPlayed;
	
	// Ranking formula
	public void CalculateRanking() {
		int winLoseDifference = wins - losses;
		int killDeathDifference = kills - deaths;
		long damageDifference = damage - damageTaken;
		
		int actualRanking = (int)(
			winLoseDifference * 2
			+ killDeathDifference * 0.1f
			+ damageDifference * 0.0001f
		);
		
		// This is the ranking we'd currently have
		int currentRanking = actualRanking + rankingOffset;
		
		// If we are below 0, reset ranking offset
		if(currentRanking < 0) {
			rankingOffset = -actualRanking;
			ranking = 0;
		// Otherwise just accept the calculated rating with its offset
		} else {
			ranking = currentRanking;
		}
	}
	
	// Calculated stats
	public double dps {get{
		if(secondsPlayed == 0)
			return 0d;
		
		return damage / secondsPlayed;
	}}
	
	public double ccpm {get{
		if(secondsPlayed == 0)
			return 0d;
		
		return 60 * cc / secondsPlayed;
	}}
	
	public int matchesPlayed {get{
		return wins + losses + leaves;
	}}
	
	public float kdRatio {get{
			return (float)kills / deaths;
	}}
	
	public float kdaRatio {get{
		if(deaths == 0)
			return 0;
		
		return (float)(kills + assists) / deaths;
	}}
	
	public string kdaString {get{
		return averageKills.ToString("0.0") + " / " + averageDeaths.ToString("0.0") + " / " + averageAssists.ToString("0.0");
	}}
	
	public float averageKills {get{
		if(matchesPlayed == 0)
			return 0;
		
		return (float)kills / matchesPlayed;
	}}
	
	public float averageDeaths {get{
		if(matchesPlayed == 0)
			return 0;
		
		return (float)deaths / matchesPlayed;
	}}
	
	public float averageAssists {get{
		if(matchesPlayed == 0)
			return 0;
		
		return (float)assists / matchesPlayed;
	}}
}
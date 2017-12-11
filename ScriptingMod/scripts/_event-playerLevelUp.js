// Example event script that announces in chat when a player leveled up
// @events playerLevelUp

importAssembly('Assembly-CSharp');

var text = "[FF6666]" + event.clientInfo.playerName + " just reached level " + event.newLevel + "!";
GameManager.Instance.GameMessageServer(null, EnumGameMessages.Chat, text, "Server", false, "", false);

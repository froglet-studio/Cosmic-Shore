using System;
using System.Collections.Generic;
using _Scripts._Core.Playfab_Models;
using _Scripts._Core.Playfab_Models.Event_Models;
using PlayFab;
using PlayFab.GroupsModels;
using StarWriter.Utility.Singleton;
using UnityEngine;


public class GroupController : SingletonPersistent<GroupController>
{
    private static PlayFabGroupsInstanceAPI _playFabGroupsInstanceAPI;
    public static event EventHandler<PlayFabError> OnErrorHandler;
    private Dictionary<string, GroupModel> groups;
    
    private void Start()
    {
        AuthenticationManager.OnLoginSuccess += InitializeGroupsInstanceAPI;
    }

    #region Initialize PlayFab Groups API with Auth Context
    
    /// <summary>
    /// Initialize PlayFab Groups API
    /// Instantiate PlayFab Groups API with auth context
    /// </summary>
    private void InitializeGroupsInstanceAPI()
    {
        if (AuthenticationManager.PlayerAccount.AuthContext == null)
        {
            // TODO: raise event to for user authentication 
            Debug.LogWarning($"Current Player has not logged in yet.");
            return;
        }

        _playFabGroupsInstanceAPI ??= new PlayFabGroupsInstanceAPI(AuthenticationManager.PlayerAccount.AuthContext);
    }
    
    #endregion
    
    
    #region Group Operations
    
    /// <summary>
    /// Create Group
    /// Create a group by a given group info
    /// <param name="groupName">Group Name</param>
    /// </summary>
    public void CreateGroup(in string groupName)
    {
        // TODO: check if the group name exists before creating
        
        _playFabGroupsInstanceAPI.CreateGroup(
            new CreateGroupRequest()
            {
                GroupName = groupName
            }, (result) =>
            {
                if (result == null) return;
                
                // Log group creation success.
                Debug.Log($"{nameof(GroupController)} - {nameof(CreateGroup)} - Group: {result.GroupName} creation success.");
                Debug.Log($"{nameof(GroupController)} - {nameof(CreateGroup)} - Group id: {result.Group.Id}.");
                
                // Create a group list cache in memory
                groups ??= new Dictionary<string, GroupModel>();
                
                // Create local group info 
                var group = new GroupModel()
                {
                    GroupName = result.GroupName,
                    Group = result.Group
                };
                
                
                // Add the newly created group info to the group list
                groups.Add(result.Group.Id, group);
                
                // TODO: The result also returns information such as group roles if it's needed somewhere
            }, 
            // Possible error code for creating a group: GroupNameNotAvailable 1368
            HandleErrorReport);
    }

    /// <summary>
    /// Delete Group
    /// Delete a group by a given group info
    /// <param name="GroupModel">Group Model</param>
    /// </summary>
    public void DeleteGroup(in GroupModel groupModel)
    {
        var groupId = groupModel.Group.Id;
        _playFabGroupsInstanceAPI.DeleteGroup(
            new DeleteGroupRequest()
            {
                Group = groupModel.Group
            }, (result) =>
            {
                if (result == null) return;
                
                // Log deleting group success
                Debug.Log($"{nameof(GroupController)} - {nameof(CreateGroup)} - group deleted.");
                groups?.Remove(groupId);

            }, 
            // No corresponding error code in the document, interesting...
            HandleErrorReport);
    }
    #endregion
    
    #region Error Handlers

    /// <summary>
    /// Handles
    /// Create a group by a given group name
    /// <param name="PlayFabError">PlayFab Error</param>
    /// </summary>
    private void HandleErrorReport(PlayFabError error)
    {
        if (error == null) return;
        
        OnErrorHandler?.Invoke(this, error);
        Debug.LogError(error.GenerateErrorReport());
    }
    #endregion
}

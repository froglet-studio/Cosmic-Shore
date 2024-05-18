using System;
using System.Collections.Generic;
using CosmicShore.Integrations.PlayFab.Authentication;
using CosmicShore.Integrations.PlayFab.Event_Models;
using CosmicShore.Utility.Singleton;
using PlayFab;
using PlayFab.GroupsModels;
using UnityEngine;
using VContainer.Unity;

namespace CosmicShore.Integrations.PlayFab.Groups
{
    public class GroupController : SingletonPersistent<GroupController>
    {
        // A public event interface for the Group front-end view
        public static event EventHandler<PlayFabError> OnErrorHandler;
    
        // Playfab Instance API
        private static PlayFabGroupsInstanceAPI _playFabGroupsInstanceAPI;
        // Event handler on getting a group info
        private static event EventHandler<GetGroupResponse> OnGettingGroup;
        // Local cache for group info
        private Dictionary<string, GroupModel> groups;

        // private AuthenticationManager _authManager;
        // public GroupController(AuthenticationManager authManager)
        // {
        //     _authManager = authManager;
        // }
        private void Start()
        {
             AuthenticationManager.OnLoginSuccess += InitializeGroupsInstanceAPI;
        }

        private void OnDestroy()
        {
            AuthenticationManager.OnLoginSuccess -= InitializeGroupsInstanceAPI;
        }

        #region Initialize PlayFab Groups API with Auth Context
    
        /// <summary>
        /// Initialize PlayFab Groups API
        /// Instantiate PlayFab Groups API with auth context
        /// </summary>
        private void InitializeGroupsInstanceAPI()
        {
            if (AuthenticationManager.PlayFabAccount.AuthContext == null)
            {
                // TODO: raise event to for user authentication 
                Debug.LogWarning($"Current Player has not logged in yet.");
                return;
            }

            _playFabGroupsInstanceAPI ??= new PlayFabGroupsInstanceAPI(AuthenticationManager.PlayFabAccount.AuthContext);
        }
    
        #endregion
    
    
        // Not sure if this entire part should be in game or in editor, just put here first
        #region Group Operations
    
        /// <summary>
        /// Create Group
        /// Create a group by a given group info
        /// <param name="groupName">Group Name</param>
        /// </summary>
        public void CreateGroup(in string groupName)
        {
            // TODO: check if the group name exists before creating (?)
        
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
                
                    // Remove the deleted group from local memory
                    // Returns false if id is not in the dictionary, no exception throws
                    groups?.Remove(groupId);

                }, 
                // No specific error code in the document, interesting...
                HandleErrorReport);
        }

        /// <summary>
        /// Get Group
        /// Delete a group by a given group info
        /// <param name="GroupModel">Group Model</param>
        /// </summary>
        private void GetGroupFromPlayFab(in string groupId)
        {
            _playFabGroupsInstanceAPI.GetGroup(
                new GetGroupRequest()
                {
                    Group = new EntityKey() { Id = groupId }
                }, (result) =>
                {
                    if (result == null) return;

                    Debug.Log($"{nameof(GroupController)} - {nameof(CreateGroup)} - group found.");
                    Debug.Log(
                        $"{nameof(GroupController)} - {nameof(CreateGroup)} - Group name: {result.GroupName} Group id: {result.Group.Id}.");
                    var group = new GroupModel()
                    {
                        GroupName = result.GroupName,
                        Group = result.Group
                    };
                    OnGettingGroup?.Invoke(this, result);
                },
                // Also no specific documented error code for getting group
                HandleErrorReport);
        }

        // public void GetGroup(out GroupModel group)
        // {
        //     return;
        // }
    
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
}

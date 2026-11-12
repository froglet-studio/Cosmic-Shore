### auth-cameras-under-cover: Authentication screen: stop the cameras that render under the opaque full-screen background -> **HOLD**

| API | Main Thread A ms | B ms | B lower in | Screen px different |
|---|---|---|---|---|

Reasons:
- editor: the bench reported an error: ERROR: the scene loaded 'Menu_Main' by itself during the bench, so nothing measured here describes it. Bench it where it stays (its real boot path, or a prefab in an empty scene).

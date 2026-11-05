// The renderer's GL is Silk.NET's binding. The engine also has a GL (UnityEngine.GL's immediate-mode API, accepted and
// not drawn), and the renderer imports both namespaces, so the name is pinned here once for every file.
global using GL = Silk.NET.OpenGL.GL;

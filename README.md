master | Project
--- | ---
[![Build status](https://ci.appveyor.com/api/projects/status/bgyngfdyprx8wqgp/branch/master?svg=true)](https://ci.appveyor.com/project/Pyrite/pyritecli/branch/master) | [![Build status](https://ci.appveyor.com/api/projects/status/bgyngfdyprx8wqgp?svg=true)](https://ci.appveyor.com/project/Pyrite/pyritecli)

# Pyrite CLI
### A tool for slicing meshes

PyriteCLI is the command line mesh and texture slicer that builds the source data for the Pyrite3D framework.  PyriteCLI makes up one of the three core parts of the framework.  The other two being the [Server](https://github.com/PyriteServer/PyriteServer) and the [Client](https://github.com/PyriteServer/PyriteDemoClient).

PyriteCLI is capable of sophisticated processing of both mesh files and textures to generate datasets which can be streamed on-demand to clients.  These capabilities can also be used for other purposes, such as slicing a large mesh into smaller chunks for direct use in Unity or WebGL applications.

#### Capabilities

+ Slice meshes in three dimensions 
+ Operate on very large meshes (100+ million vertices)
+ Dissassemble and reassemble packed texture files into multiple smaller texture files
+ Wavefront OBJ + JPG input
+ OBJ, CTM, and EBO output
+ Distributed cloud processing support for advanced users

#### Tutorials

Simple slicing of an OBJ and texture file
[Video on Youtube](https://www.youtube.com/watch?v=49oem-evWCU&feature=youtu.be)


#### Usage
```PyriteCli --help```

# temporary solution to getting project path in generator

import os
import sys

if len(sys.argv) != 2:
    print("You must provide a path to your project for Owl Tree to set itself up.")

ex_path = sys.argv[0]
proj_path = sys.argv[1]
frame_path = os.getcwd()
frame_path = frame_path.replace('\\', '/')

env_file_name = "OwlTreeEnv.cs"
env_file_data = """
namespace OwlTree
{
    public static class OwlTreeEnv
    {
        public const string ProjectPath = "p_path";
        public const string FrameworkPath = "f_path";
    } 
}"""
env_file_data = env_file_data.replace("p_path", proj_path).replace("f_path", frame_path)

with open(proj_path + "/" + env_file_name, "w") as env_file:
    env_file.write(env_file_data)
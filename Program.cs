using Assimp;
using static Infinite_module_test.tag_structs;
using static System.Net.Mime.MediaTypeNames;

namespace Infinite_model_importer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

        }

        static tag load_tag(string file)
        {
            if (!File.Exists(file)) throw new Exception("file does not exist");
            byte[] file_bytes = File.ReadAllBytes(file);

            tag test = new tag(new List<KeyValuePair<byte[], bool>>()); // apparently we do NOT support null, despite declaring it as nullable (this is for compiling at least)
            if (!test.Load_tag_file(file_bytes)) throw new Exception("failed to load tag");
            return test;
        }
        static void convert_model_to_tag(string input_model_path, string input_template_tag, string output_tag_path)
        {
            // load the template tag
            tag rtgo_tag = load_tag("template\\template.bitm");
            rtgo_tag.set_tagID(tagid); // we have to update the tagID, as we're going to have the template set to -1



            // load the 3d model
            Assimp.Scene model;
            Assimp.AssimpContext importer = new Assimp.AssimpContext();
            importer.SetConfig(new Assimp.Configs.NormalSmoothingAngleConfig(66.0f));
            model = importer.ImportFile(input_model_path, Assimp.PostProcessPreset.TargetRealTimeMaximumQuality);

            // for now the ideal thing we should do here is just bunch all our stuff into a single mesh part
            // or actually, we're just going to operate on the first mesh, assuming its the one we actually want to import

            for (int i = 0; i < model.MeshCount; i++){
                if (i > 0) continue; // ONLY DOING THE FIRST MESH FOR NOW!!!

                var mesh = model.Meshes[i];
                var verts = ;
                var tangents = mesh.Tangents;
                mesh.Normals;
                mesh.VertexColorChannels; // im pretty sure this is 
                mesh.TextureCoordinateChannels; // UVs?
                var indices = mesh.GetIndices();

                // copy over base mesh info
                rtgo_tag.set_number("render geometry.meshes[0].LOD render data[0].parts[0].index count", indices.Length.ToString());
                rtgo_tag.set_number("render geometry.meshes[0].LOD render data[0].parts[0].budget vertex count", mesh.VertexCount.ToString());

                // preprocess vertex positions to find largest distance so we can normalize
                float highest_distance = 0.0f;
                foreach(var v in mesh.Vertices){
                    if (Math.Abs(v.X) > highest_distance)
                        highest_distance = Math.Abs(v.X);
                    if (Math.Abs(v.Y) > highest_distance)
                        highest_distance = Math.Abs(v.Y);
                    if (Math.Abs(v.Z) > highest_distance)
                        highest_distance = Math.Abs(v.Z);
                }
                 
                // now we need to start compiling the tag
                // open stream writer to start writing out the chunk data
                int current_offset = 0;
                var vertex_block = rtgo_tag.get_tagblock("render geometry.mesh package.mesh resource groups[0].mesh resource.pc vertex buffers");
                using (FileStream fs = new FileStream(output_tag_path + "_res_0", FileMode.Create))
                using (BinaryWriter bw = new BinaryWriter(fs)){
                    // start with vertex positions (8 bytes wide)
                    current_offset += fill_in_buffer_details(rtgo_tag, mesh.VertexCount, 8, current_offset, vertex_block, 0);
                    foreach (var v in mesh.Vertices){
                        // normalize & pack each vector // NOTE: we may have to prevent the endianness flipping when writing the bytes?
                        float normalized_x = v.X / highest_distance; // pretty sure this doesn't care about negatives or whatever
                        short packed_x = (short)(normalized_x * 0xffff);
                        bw.Write(packed_x);

                        float normalized_y = v.Y / highest_distance; // pretty sure this doesn't care about negatives or whatever
                        short packed_y = (short)(normalized_y * 0xffff);
                        bw.Write(packed_y);

                        float normalized_z = v.Z / highest_distance; // pretty sure this doesn't care about negatives or whatever
                        short packed_z = (short)(normalized_z * 0xffff);
                        bw.Write(packed_z);

                        bw.Write(new byte[2] {0,0}); // pad out the last guy?
                    }
                    // it goes in this order
                    // 0: position
                    // 1: UV0
                    // 2: Normal
                    // 3: Tangent
                    // 4: Color






                    // and then we finish with the index buffers

                }



                tag.compiled_tag output = rtgo_tag.compile();
                // we aren't going to output resources with this, so we only need to worry about spitting out the main file
                File.WriteAllBytes(output_tag_path, output.tag_bytes);

                // we're actually going to keep the resource chunk as a separate process when we write it
                // this avoids complicated things
            }

        }
        static int fill_in_buffer_details(tag rtgo_tag, int entry_count, int entry_width, int current_offset, tag.tagdata_struct buffer_block, int buffer_index){
            rtgo_tag.set_number("count", entry_count.ToString(), buffer_block.blocks[buffer_index], buffer_block.GUID);
            rtgo_tag.set_number("offset", current_offset.ToString(), buffer_block.blocks[buffer_index], buffer_block.GUID);
            rtgo_tag.set_number("d3dbuffer.byte width", (entry_width * entry_count).ToString(), buffer_block.blocks[buffer_index], buffer_block.GUID);
            return entry_width * entry_count;
        }
    }
}
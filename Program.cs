using Assimp;
using Infinite_module_test;
using static Infinite_module_test.tag_structs;
using static System.Net.Mime.MediaTypeNames;

namespace Infinite_model_importer
{
    internal class Program
    {
        static void Main(string[] args) 
        {
            Console.WriteLine("Hello, World!");
            convert_model_to_tag("C:\\Users\\Joe bingle\\Downloads\\model testing\\shrek3.obj",
                                 "C:\\Users\\Joe bingle\\Downloads\\model testing\\template2.rtgo",
                                 0x24832954,
                                 "C:\\Users\\Joe bingle\\Downloads\\model testing\\shrek3.rtgo");
        }

        static tag load_tag(string file)
        {
            if (!File.Exists(file)) throw new Exception("file does not exist");
            byte[] file_bytes = File.ReadAllBytes(file);

            tag test = new tag(new List<KeyValuePair<byte[], bool>>()); // apparently we do NOT support null, despite declaring it as nullable (this is for compiling at least)
            if (!test.Load_tag_file(file_bytes)) throw new Exception("failed to load tag");
            return test;
        }
        static void convert_model_to_tag(string input_model_path, string input_template_tag, uint new_tagid, string output_tag_path){
            // load the template tag
            tag rtgo_tag = load_tag(input_template_tag);
            rtgo_tag.set_tagID(new_tagid); // we have to update the tagID, as we're going to have the template set to -1



            // load the 3d model
            Assimp.Scene model;
            Assimp.AssimpContext importer = new Assimp.AssimpContext();
            //importer.SetConfig(new Assimp.Configs.NormalSmoothingAngleConfig(66.0f));
            model = importer.ImportFile(input_model_path, 0); // Assimp.PostProcessPreset.TargetRealTimeMaximumQuality);

            // for now the ideal thing we should do here is just bunch all our stuff into a single mesh part
            // or actually, we're just going to operate on the first mesh, assuming its the one we actually want to import

            for (int i = 0; i < model.MeshCount; i++){
                if (i > 0) continue; // ONLY DOING THE FIRST MESH FOR NOW!!!

                var mesh = model.Meshes[i];
                var indices = mesh.GetIndices();

                // verify the states of each vertex type
                bool invalid_verts = (mesh.Vertices.Count == 0);
                bool invalid_UV0 = (mesh.TextureCoordinateChannels.Length == 0 || mesh.TextureCoordinateChannels[0].Count == 0);
                bool invalid_normals = (mesh.Normals.Count == 0);
                bool invalid_tangents = (mesh.Tangents.Count == 0);
                bool invalid_colors = (mesh.VertexColorChannels.Length == 0 || mesh.VertexColorChannels[0].Count == 0);

                if (invalid_verts) throw new Exception("why would this even be invalid");
                // error check to make sure all buffers have the exact same number of entries
                if (!invalid_verts && mesh.Vertices.Count != mesh.VertexCount)                   throw new Exception("bad vertex position count");
                if (!invalid_UV0 && mesh.TextureCoordinateChannels[0].Count != mesh.VertexCount) throw new Exception("bad vertex UV's count");
                if (!invalid_normals && mesh.Normals.Count != mesh.VertexCount)                  throw new Exception("bad vertex normals count");
                if (!invalid_tangents && mesh.Tangents.Count != mesh.VertexCount)                throw new Exception("bad vertex tangents count");
                if (!invalid_colors && mesh.VertexColorChannels[0].Count != mesh.VertexCount)    throw new Exception("bad vertex colors count");


                // copy over base mesh info
                rtgo_tag.set_number("render geometry.meshes[0].LOD render data[0].parts[0].index count", indices.Length.ToString());
                rtgo_tag.set_number("render geometry.meshes[0].LOD render data[0].parts[0].budget vertex count", mesh.VertexCount.ToString());
                // raytracing metadata
                rtgo_tag.set_number("render geometry.mesh package.mesh resource groups[0].mesh resource.raytracing metadata[0].mesh part metadata[0].index count", indices.Length.ToString());


                // preprocess vertex positions to find largest distance so we can normalize
                // we should fill in the compression data bounds?
                // this may actually be wrong. // NOTE: halo uses x y & z around different orders than we do, so time to swap them around until they work
                float bound_x = 0.0f;
                float bound_x_minus = 0.0f;
                float bound_y = 0.0f;
                float bound_y_minus = 0.0f;
                float bound_z = 0.0f;
                float bound_z_minus = 0.0f;

                float highest_distance = 0.0f;
                foreach(var v in mesh.Vertices){
                    if (Math.Abs(v.X) > highest_distance) highest_distance = Math.Abs(v.X);
                    if (Math.Abs(v.Y) > highest_distance) highest_distance = Math.Abs(v.Y);
                    if (Math.Abs(v.Z) > highest_distance) highest_distance = Math.Abs(v.Z);

                    if (v.X > bound_x)       bound_x = v.X;
                    if (v.X < bound_x_minus) bound_x_minus = v.X;
                    if (v.Y > bound_y)       bound_y = v.Y;
                    if (v.Y < bound_y_minus) bound_y_minus = v.Y;
                    if (v.Z > bound_z)       bound_z = v.Z;
                    if (v.Z < bound_z_minus) bound_z_minus = v.Z;
                }
                // update compression & per part data
                rtgo_tag.set_float3("Per Mesh Data[0].Bounds min", new(bound_x, bound_y, bound_z));
                rtgo_tag.set_float3("Per Mesh Data[0].Bounds max", new(bound_x_minus, bound_y_minus, bound_z_minus));

                rtgo_tag.set_float3("render geometry.compression info[0].position bounds 0", new(bound_x, bound_x_minus, bound_y));
                rtgo_tag.set_float3("render geometry.compression info[0].position bounds 1", new(bound_y_minus, bound_z, bound_z_minus));

                // preprocess UV bounds
                float uv_bound_x = 0.0f;
                float uv_bound_x_minus = 0.0f;
                float uv_bound_y = 0.0f;
                float uv_bound_y_minus = 0.0f;
                float uv_bound_z = 0.0f;
                float uv_bound_z_minus = 0.0f;

                float highest_uv0_distance = 0.0f;
                // most amount of UV channels is 3 i believe // even though we're just going to support the first channel
                if (mesh.TextureCoordinateChannelCount > 3) throw new Exception("exceeded maximum supported UV channels count! we can still process the first 3 however");
                foreach (var v in mesh.TextureCoordinateChannels[0]){
                    if (Math.Abs(v.X) > highest_uv0_distance) highest_uv0_distance = Math.Abs(v.X);
                    if (Math.Abs(v.Y) > highest_uv0_distance) highest_uv0_distance = Math.Abs(v.Y);

                    if (v.X > uv_bound_x)       uv_bound_x = v.X;
                    if (v.X < uv_bound_x_minus) uv_bound_x_minus = v.X;
                    if (v.Y > uv_bound_y)       uv_bound_y = v.Y;
                    if (v.Y < uv_bound_y_minus) uv_bound_y_minus = v.Y;
                    if (v.Z > uv_bound_z)       uv_bound_z = v.Z;
                    if (v.Z < uv_bound_z_minus) uv_bound_z_minus = v.Z;
                }


                // now we need to start compiling the tag
                // open stream writer to start writing out the chunk data
                int current_offset = 0;
                var vertex_block = rtgo_tag.get_tagblock("render geometry.mesh package.mesh resource groups[0].mesh resource.pc vertex buffers");
                var index_block = rtgo_tag.get_tagblock("render geometry.mesh package.mesh resource groups[0].mesh resource.pc index buffers");

                using (FileStream fs = new FileStream(output_tag_path + "_res_0", FileMode.Create))
                using (BinaryWriter bw = new BinaryWriter(fs)){
                    // start with vertex positions (8 bytes wide)
                    current_offset += fill_in_buffer_details(rtgo_tag, mesh.VertexCount, 8, current_offset, vertex_block, 0);
                    foreach (var v in mesh.Vertices){
                        // normalize & pack each vector
                        float normalized_x = ((v.X / highest_distance) + 1) / 2; 
                        ushort packed_x = (ushort)(normalized_x * 0xffff);
                        bw.Write(packed_x);
                        float unpacked_x = (((float)(ushort)packed_x / 0xffff) * 2.0f) - 1.0f;

                        float normalized_y = ((v.Y / highest_distance) + 1) / 2;
                        ushort packed_y = (ushort)(normalized_y * 0xffff);
                        bw.Write(packed_y);
                        float unpacked_y = (((float)(ushort)packed_y / 0xffff) * 2.0f) - 1.0f;

                        float normalized_z = ((v.Z / highest_distance) + 1) / 2;
                        ushort packed_z = (ushort)(normalized_z * 0xffff);
                        bw.Write(packed_z);
                        float unpacked_z = (((float)(ushort)packed_z / 0xffff) * 2.0f) - 1.0f;

                        bw.Write(new byte[2] {0,0}); // pad out the last guy?
                    }
                    // UV0s (4 bytes wide)
                    current_offset += fill_in_buffer_details(rtgo_tag, mesh.VertexCount, 4, current_offset, vertex_block, 1);
                    if (invalid_UV0) for (int p = 0; p < mesh.VertexCount; p++)bw.Write(0);
                    else foreach (var v in mesh.TextureCoordinateChannels[0]){
                        // normalize & pack each vector // NOTE: we may have to prevent the endianness flipping when writing the bytes?
                        float normalized_x = ((v.X / highest_uv0_distance) + 1) / 2;
                        bw.Write((short)(normalized_x * 0xffff));

                        float normalized_y = ((v.Y / highest_uv0_distance) + 1) / 2;
                        bw.Write((short)(normalized_y * 0xffff));
                    }

                    // normals (4 bytes wide)
                    current_offset += fill_in_buffer_details(rtgo_tag, mesh.VertexCount, 4, current_offset, vertex_block, 2);
                    if (invalid_normals) for (int p = 0; p < mesh.VertexCount; p++) bw.Write(0);
                    else foreach (var v in mesh.Normals){
                        // we have to pack this all into a single int to write
                        // make sure the normals work how we think they work
                        if (v.X > 1.0f || v.X < -1.0f || v.Y > 1.0f || v.Y < -1.0f || v.Z > 1.0f || v.Z < -1.0f) throw new Exception("out of bounds normal value?");
                        ushort packed_x = (ushort)( ((v.X + 1.0f) / 2.0f) * 0x3FF); // uhh i think the conversion will be fine here
                        ushort packed_y = (ushort)( ((v.Y + 1.0f) / 2.0f) * 0x3FF); 
                        ushort packed_z = (ushort)( ((v.Z + 1.0f) / 2.0f) * 0x3FF);

                        // this is how they're packed
                        // buffer[(i * 3)] = ((float)(block & 0x3ff) / 1023u - 0.5f) * 2;

                        uint packed = ((uint)packed_x & 0x3ff) | (((uint)packed_y & 0x3ff) << 10) | (((uint)packed_z & 0x3ff) << 20);
                        bw.Write(packed);
                    }

                    // tangents (4 bytes wide)
                    current_offset += fill_in_buffer_details(rtgo_tag, mesh.VertexCount, 4, current_offset, vertex_block, 3);
                    if (invalid_tangents) for (int p = 0; p < mesh.VertexCount; p++) bw.Write(0);
                    else foreach (var v in mesh.Tangents){
                        // make sure tangents work how we think they work
                        if (v.X > 1.0f || v.X < -1.0f ||  v.Y > 1.0f || v.Y < -1.0f || v.Z > 1.0f || v.Z < -1.0f) throw new Exception("out of bounds normal value?");
                        bw.Write((byte)( ((v.X + 1.0f) / 2.0f) * 0xFF));
                        bw.Write((byte)( ((v.Y + 1.0f) / 2.0f) * 0xFF));
                        bw.Write((byte)( ((v.Z + 1.0f) / 2.0f) * 0xFF));
                    }

                    // bytecolors (4 bytes wide)
                    current_offset += fill_in_buffer_details(rtgo_tag, mesh.VertexCount, 4, current_offset, vertex_block, 4);
                    if (invalid_colors) for (int p = 0; p < mesh.VertexCount; p++) bw.Write(0);
                    else foreach (var v in mesh.VertexColorChannels[0]){
                        bw.Write((byte)(v.A * 255));
                        bw.Write((byte)(v.R * 255));
                        bw.Write((byte)(v.G * 255));
                        bw.Write((byte)(v.B * 255));
                    }

                    // and then we finish with the index buffers
                    current_offset += fill_in_buffer_details(rtgo_tag, indices.Length, 4, current_offset, index_block, 0);
                    foreach (var v in indices) 
                        bw.Write(v);
                }

                // we can now fill in the buffer size details
                rtgo_tag.set_number("render geometry.mesh package.mesh resource groups[0].mesh resource.Streaming Chunks[0].buffer end", current_offset.ToString());
                rtgo_tag.set_number("render geometry.mesh package.mesh resource groups[0].mesh resource.Streaming Buffers[0].buffer size", current_offset.ToString());


                tag.compiled_tag output = rtgo_tag.compile();
                // we aren't going to output resources with this, so we only need to worry about spitting out the main file
                File.WriteAllBytes(output_tag_path, output.tag_bytes);
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
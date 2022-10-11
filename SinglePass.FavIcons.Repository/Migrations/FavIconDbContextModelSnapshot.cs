﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SinglePass.FavIcons.Repository;

#nullable disable

namespace SinglePass.FavIcons.Repository.Migrations
{
    [DbContext(typeof(FavIconDbContext))]
    partial class FavIconDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "6.0.9");

            modelBuilder.Entity("SinglePass.FavIcons.Application.FavIcon", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<byte[]>("Bytes")
                        .HasColumnType("BLOB");

                    b.Property<string>("Host")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("FavIcons");
                });
#pragma warning restore 612, 618
        }
    }
}

<script setup lang="ts">
import { useRoute } from 'vue-router'

const route = useRoute()
const isHome = () => route.name === 'Home'
</script>

<template>
  <div class="app-container">
    <!-- Navigation -->
    <nav class="navbar">
      <div class="nav-brand">
        <h1>Unified Scheduling</h1>
      </div>
      <ul class="nav-links">
        <li>
          <RouterLink to="/dashboard" active-class="active">Dashboard</RouterLink>
        </li>
        <li>
          <RouterLink to="/training" active-class="active">Training</RouterLink>
        </li>
        <li>
          <RouterLink to="/users" active-class="active">Users</RouterLink>
        </li>
      </ul>
    </nav>

    <div v-if="isHome()" class="welcome-section">
      <h1>Welcome to Unified Scheduling Application</h1>
      <p>Select a section from the navigation menu to get started.</p>
    </div>
    <!-- Main Content -->
    <main class="main-content">
      <RouterView />
    </main>
  </div>
</template>

<style scoped></style>

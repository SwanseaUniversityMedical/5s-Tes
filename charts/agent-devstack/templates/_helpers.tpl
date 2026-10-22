{{/*
Expand the name of the chart.
*/}}
{{- define "agent-devstack.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
Part of the standard scaffold; devstack templates do not call it.
*/}}
{{- define "agent-devstack.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Chart name and version, used by the chart label.
*/}}
{{- define "agent-devstack.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels.
*/}}
{{- define "agent-devstack.labels" -}}
helm.sh/chart: {{ include "agent-devstack.chart" . }}
{{ include "agent-devstack.selectorLabels" . }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels.
*/}}
{{- define "agent-devstack.selectorLabels" -}}
app.kubernetes.io/name: {{ include "agent-devstack.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}
